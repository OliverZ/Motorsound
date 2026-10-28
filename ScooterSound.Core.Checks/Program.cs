using ScooterSound.Core;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

foreach (var profile in EngineProfile.All)
{
    var engine = new EngineSimulator();
    EngineSnapshot snapshot = engine.Tick(0, profile);
    Check(snapshot.State == DrivingState.Idle && snapshot.SignalLost, "Initial state must be idle without signal.");
    for (int i = 1; i <= 250; i++)
    {
        double t = i * .1;
        engine.PushSpeed(Math.Min(6, t * .5), 5, t, t);
        snapshot = engine.Tick(t, profile);
        Check(snapshot.Rpm >= profile.IdleRpm && snapshot.Rpm <= profile.MaxRpm, "RPM out of range.");
        if (i == 60) Check(snapshot.State == DrivingState.Accelerating, "Acceleration must be detected.");
    }
    Check(snapshot.SpeedKmh > 20, "Steady ride not reached.");
    Check(snapshot.State == DrivingState.Cruising, "Steady speed must settle to cruising.");
    Check(!engine.PushSpeed(30, 5, 25.1, 25.1), "GPS spike must be rejected.");
    Check(!engine.PushSpeed(6, 100, 25.2, 25.2), "Inaccurate GPS must be rejected.");
    Check(!engine.PushSpeed(6, 5, 20, 25.2), "Stale GPS must be rejected.");
    Check(!engine.PushSpeed(double.NaN, 5, 25.2, 25.2), "NaN must be rejected.");
    for (int i = 251; i <= 400; i++) snapshot = engine.Tick(i * .1, profile);
    Check(snapshot.SignalLost && snapshot.SpeedKmh < .5, "GPS loss must settle to idle.");
    Check(snapshot.State == DrivingState.Idle, "GPS loss must end in idle state.");

    var synth = new Synthesizer();
    short[] pcm = new short[44100];
    synth.Render(pcm, snapshot, profile.Motor, .6);
    Check(pcm.Any(v => v != 0), "Motor must produce audio.");
    Check(pcm.All(v => Math.Abs((int)v) < short.MaxValue), "Audio must not clip.");
    synth.Render(pcm, snapshot, profile.Motor, 0);
    synth.Render(pcm, snapshot, profile.Motor, 0);
    Check(pcm.TakeLast(1000).All(v => v == 0), "Volume zero must settle to silence.");
    Console.WriteLine($"PASS {profile.Name}: speed, GPS failure, RPM and PCM checks");
}

var braking = new EngineSimulator();
var motor = EngineProfile.All[2];
for (int i = 0; i < 100; i++) { braking.PushSpeed(6, 5, i * .1, i * .1); braking.Tick(i * .1, motor); }
EngineSnapshot result = braking.Tick(9.9, motor);
for (int i = 100; i <= 120; i++)
{
    double t = i * .1;
    braking.PushSpeed(Math.Max(0, 6 - (t - 10) * 1.5), 5, t, t);
    result = braking.Tick(t, motor);
}
Check(result.State == DrivingState.Decelerating, "Braking must be detected.");
Console.WriteLine("PASS braking detection");
