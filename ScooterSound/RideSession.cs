using ScooterSound.Core;

namespace ScooterSound;

// Immutable snapshots cross the UI/service/audio thread boundary.
public static class RideSession
{
    static EngineSnapshot snapshot = new(0, 0, 800, 0, 0, DrivingState.Idle, true);
    static EngineProfile profile = EngineProfile.DisplayOrder[0];
    static AudioSettings audio = new AudioSettings(
        Preferences.Default.Get("volume", 1d), Preferences.Default.Get("boostDb", 6d),
        Preferences.Default.Get("overdrive", 0d)).Normalize();
    static double testSpeed;
    static double throttle;
    static string status = "Bereit";
    static int running;
    public static EngineSnapshot Snapshot { get => Volatile.Read(ref snapshot); set => Volatile.Write(ref snapshot, value); }
    public static EngineProfile Profile { get => Volatile.Read(ref profile); set => Volatile.Write(ref profile, value); }
    public static AudioSettings Audio => Volatile.Read(ref audio);
    public static double Volume { get => Audio.Volume; set => UpdateAudio(s => s with { Volume = value }); }
    public static double BoostDb { get => Audio.BoostDb; set => UpdateAudio(s => s with { BoostDb = value }); }
    public static double Overdrive { get => Audio.Overdrive; set => UpdateAudio(s => s with { Overdrive = value }); }
    static void UpdateAudio(Func<AudioSettings, AudioSettings> update)
    {
        AudioSettings before, after;
        do
        {
            before = Audio;
            after = update(before).Normalize();
        } while (!ReferenceEquals(Interlocked.CompareExchange(ref audio, after, before), before));
    }
    public static void SaveAudio()
    {
        var settings = Audio;
        Preferences.Default.Set("volume", settings.Volume);
        Preferences.Default.Set("boostDb", settings.BoostDb);
        Preferences.Default.Set("overdrive", settings.Overdrive);
    }
    public static double TestSpeed { get => Volatile.Read(ref testSpeed); set => Volatile.Write(ref testSpeed, value); }
    public static double Throttle
    {
        get => Running ? Volatile.Read(ref throttle) : 0;
        set => Volatile.Write(ref throttle, double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0);
    }
    public static string Status { get => Volatile.Read(ref status); set => Volatile.Write(ref status, value); }
    public static bool Running { get => Volatile.Read(ref running) == 1; set => Volatile.Write(ref running, value ? 1 : 0); }
}
