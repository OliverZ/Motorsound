using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Locations;
using Android.Media;
using Android.OS;
using ScooterSound.Core;
using ALocation = Android.Locations.Location;

namespace ScooterSound;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation | ForegroundService.TypeMediaPlayback)]
public sealed class RideService : Service, ILocationListener, AudioManager.IOnAudioFocusChangeListener
{
    const int NotificationId = 42;
    const string ChannelId = "ride";
    readonly object gate = new();
    readonly EngineSimulator simulator = new();
    CancellationTokenSource? cancellation;
    LocationManager? locations;
    AudioManager? audioManager;
    AudioFocusRequestClass? focus;
    PowerManager.WakeLock? wakeLock;
    HandlerThread? locationThread;
    HandlerExecutor? locationExecutor;
    NoisyReceiver? noisy;
    bool receiverRegistered;
    ALocation? previousLocation;
    bool testMode;
    volatile bool destroyed;
    string? endMessage;
    static double Now => SystemClock.ElapsedRealtime() / 1000d;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == "stop") { StopSelf(); return StartCommandResult.NotSticky; }
        if (cancellation is not null) return StartCommandResult.NotSticky;
        testMode = intent?.GetBooleanExtra("test", false) == true;
        try
        {
            CreateNotification();
            audioManager = (AudioManager)GetSystemService(AudioService)!;
            var attributes = new AudioAttributes.Builder()!.SetUsage(AudioUsageKind.Media)!
                .SetContentType(AudioContentType.Music)!.Build()!;
            focus = new AudioFocusRequestClass.Builder(AudioFocus.Gain)!
                .SetAudioAttributes(attributes)!
                .SetWillPauseWhenDucked(true)!
                .SetOnAudioFocusChangeListener(this, new Handler(Looper.MainLooper!))!.Build();
            if (audioManager.RequestAudioFocus(focus!) != AudioFocusRequest.Granted)
                throw new InvalidOperationException("Audioausgabe wird gerade von einer anderen App verwendet.");
            noisy = new NoisyReceiver(() => EndRide("Audioverbindung getrennt – Fahrt gestoppt"));
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
                RegisterReceiver(noisy, new IntentFilter(AudioManager.ActionAudioBecomingNoisy), ReceiverFlags.NotExported);
            else
                RegisterReceiver(noisy, new IntentFilter(AudioManager.ActionAudioBecomingNoisy));
            receiverRegistered = true;
            // Hold the CPU before registering GPS; neither depends on the Activity
            // or its refresh timer, and the display is free to switch off.
            var power = (PowerManager)GetSystemService(PowerService)!;
            wakeLock = power.NewWakeLock(WakeLockFlags.Partial, "ScooterSound:Ride");
            wakeLock!.Acquire();
            if (!testMode)
            {
                locations = (LocationManager)GetSystemService(LocationService)!;
                locationThread = new HandlerThread("ScooterSound:GPS");
                locationThread.Start();
                if (OperatingSystem.IsAndroidVersionAtLeast(31))
                {
                    locationExecutor = new HandlerExecutor(new Handler(locationThread.Looper!));
                    using var request = new LocationRequest.Builder(500)
                        .SetMinUpdateIntervalMillis(250)!
                        .SetMaxUpdateDelayMillis(0)!
                        .SetMinUpdateDistanceMeters(0)!
                        .SetQuality((int)LocationRequestQuality.HighAccuracy)!.Build()!;
                    locations.RequestLocationUpdates(LocationManager.GpsProvider, request, locationExecutor, this);
                }
                else locations.RequestLocationUpdates(LocationManager.GpsProvider, 500, 0, this, locationThread.Looper);
            }
            RideSession.Throttle = 0;
            RideSession.Running = true;
            RideSession.Status = testMode ? "Testmodus · Geschwindigkeit über den Regler" : "Warte auf GPS …";
            cancellation = new CancellationTokenSource();
            var token = cancellation.Token;
            _ = Task.Run(() => AudioLoop(token));
        }
        catch (Exception ex) { EndRide($"Start fehlgeschlagen: {ex.Message}"); }
        return StartCommandResult.NotSticky;
    }

    void CreateNotification()
    {
        var manager = (NotificationManager)GetSystemService(NotificationService)!;
        manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Aktive Fahrt", NotificationImportance.Low));
        var open = PendingIntent.GetActivity(this, 0, new Intent(this, typeof(MainActivity)), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var stop = PendingIntent.GetService(this, 1, new Intent(this, typeof(RideService)).SetAction("stop"), PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);
        var notification = new Notification.Builder(this, ChannelId)
            .SetContentTitle("Scooter Sound läuft")!
            .SetContentText(testMode ? "Testmodus aktiv" : "GPS und Motorsound aktiv")!
            .SetSmallIcon(global::Android.Resource.Drawable.IcMediaPlay)!
            .SetContentIntent(open)!.SetOngoing(true)!
            .AddAction(new Notification.Action.Builder(global::Android.Resource.Drawable.IcMediaPause, "Stoppen", stop).Build())!
            .Build();
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
            StartForeground(NotificationId, notification,
                testMode ? ForegroundService.TypeMediaPlayback : ForegroundService.TypeLocation | ForegroundService.TypeMediaPlayback);
        else StartForeground(NotificationId, notification);
    }

    void AudioLoop(CancellationToken token)
    {
        try
        {
            global::Android.OS.Process.SetThreadPriority(global::Android.OS.ThreadPriority.Audio);
            const int rate = 44100;
            int bufferBytes = Math.Max(4096, AudioTrack.GetMinBufferSize(rate, ChannelOut.Mono, Encoding.Pcm16bit));
            using var track = new AudioTrack(global::Android.Media.Stream.Music, rate, ChannelOut.Mono, Encoding.Pcm16bit, bufferBytes, AudioTrackMode.Stream);
            if (track.State != AudioTrackState.Initialized) throw new InvalidOperationException("AudioTrack konnte nicht initialisiert werden.");
            track.SetVolume(1);
            var synth = new Synthesizer();
            synth.Start();
            var buffer = new short[882]; // 20 ms blocks, distinct from the device's output buffer.
            double lastTest = double.NegativeInfinity;
            double testMetresPerSecond = 0;
            track.Play();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    double now = Now;
                    EngineSnapshot snapshot;
                    var profile = RideSession.Profile;
                    lock (gate)
                    {
                        if (testMode && now - lastTest >= .1)
                        {
                            double step = double.IsFinite(lastTest) ? Math.Min(now - lastTest, .5) * 3 : 0;
                            testMetresPerSecond += Math.Clamp(RideSession.TestSpeed / 3.6 - testMetresPerSecond, -step, step);
                            simulator.PushSpeed(testMetresPerSecond, 0, now, now);
                            lastTest = now;
                        }
                        snapshot = simulator.Tick(now, profile, RideSession.Throttle);
                    }
                    if (token.IsCancellationRequested) break;
                    RideSession.Snapshot = snapshot;
                    if (!testMode) RideSession.Status = snapshot.SignalLost
                        ? RideSession.Throttle > 0 ? "Kein aktuelles GPS · Gaspedal aktiv" : "Kein aktuelles GPS · Übergang zum Leerlauf"
                        : "GPS aktiv · Geschwindigkeit geglättet";
                    synth.Render(buffer, snapshot, profile.Motor, RideSession.Audio);
                    int offset = 0;
                    while (offset < buffer.Length && !token.IsCancellationRequested)
                    {
                        int written = track.Write(buffer, offset, buffer.Length - offset);
                        if (written <= 0) throw new InvalidOperationException($"Audioausgabe unterbrochen ({written}).");
                        offset += written;
                    }
                }
            }
            finally { track.Stop(); track.Flush(); }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                new Handler(Looper.MainLooper!).Post(() => { if (!destroyed) EndRide($"Audiofehler: {ex.Message}"); });
        }
    }

    public void OnLocationChanged(ALocation location)
    {
        // Callbacks now run on the GPS thread. Serialize teardown and simulator
        // input so a final queued fix cannot access a disposed location.
        lock (gate)
        {
            if (destroyed) return;
            AcceptLocation(location);
        }
    }

    void AcceptLocation(ALocation location)
    {
        double time = location.ElapsedRealtimeNanos / 1_000_000_000d;
        double now = Now;
        if (!location.HasAccuracy || location.Accuracy > 40 || now - time > 3 || time > now + .5) return;
        double metresPerSecond;
        if (location.HasSpeed) metresPerSecond = location.Speed;
        else
        {
            if (previousLocation is null) { previousLocation = new ALocation(location); return; }
            double dt = (location.ElapsedRealtimeNanos - previousLocation.ElapsedRealtimeNanos) / 1_000_000_000d;
            if (dt < 1) return;
            // Position deltas near the accuracy radius are unreliable at scooter speeds.
            if (dt > 5 || location.Accuracy > 15 || previousLocation.Accuracy > 15)
            { previousLocation.Dispose(); previousLocation = new ALocation(location); return; }
            double distance = location.DistanceTo(previousLocation);
            metresPerSecond = distance < Math.Max(2, (location.Accuracy + previousLocation.Accuracy) * .5) ? 0 : distance / dt;
        }
        previousLocation?.Dispose();
        previousLocation = new ALocation(location);
        simulator.PushSpeed(metresPerSecond, location.Accuracy, time, now);
    }

    public void OnProviderDisabled(string provider) { RideSession.Status = "GPS deaktiviert · Übergang zum Leerlauf"; }
    public void OnProviderEnabled(string provider) { }
    public void OnStatusChanged(string? provider, Availability status, Bundle? extras) { }
    public void OnAudioFocusChange(AudioFocus focusChange)
    {
        if (focusChange is AudioFocus.Loss or AudioFocus.LossTransient or AudioFocus.LossTransientCanDuck)
            EndRide("Audio unterbrochen · Fahrt zum Fortsetzen erneut starten");
    }
    void EndRide(string message) { endMessage = message; StopSelf(); }

    public override void OnDestroy()
    {
        destroyed = true;
        RideSession.Throttle = 0;
        cancellation?.Cancel();
        try { locations?.RemoveUpdates(this); }
        catch (Java.Lang.SecurityException) { /* Location permission may have been revoked mid-ride. */ }
        locationThread?.QuitSafely();
        lock (gate)
        {
            previousLocation?.Dispose();
            previousLocation = null;
        }
        if (noisy is not null)
        {
            if (receiverRegistered) UnregisterReceiver(noisy);
            noisy.Dispose();
        }
        if (focus is not null) { audioManager?.AbandonAudioFocusRequest(focus); focus.Dispose(); }
        if (wakeLock?.IsHeld == true) wakeLock.Release();
        wakeLock?.Dispose();
        cancellation?.Dispose();
        RideSession.Running = false;
        RideSession.Status = endMessage ?? "Fahrt beendet";
        StopForeground(StopForegroundFlags.Remove);
        base.OnDestroy();
    }

    sealed class NoisyReceiver(Action disconnected) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == AudioManager.ActionAudioBecomingNoisy) disconnected();
        }
    }

    sealed class HandlerExecutor(Handler handler) : Java.Lang.Object, Java.Util.Concurrent.IExecutor
    {
        public void Execute(Java.Lang.IRunnable? command)
        {
            if (command is not null) handler.Post(command);
        }
    }
}
