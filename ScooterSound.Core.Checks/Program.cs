using ScooterSound.Core;
using System.Numerics;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

Check(EngineProfile.DisplayOrder.Select(p => p.Motor).SequenceEqual(new[]
{
    Motor.PlymouthV8, Motor.V8, Motor.Diesel, Motor.Motorcycle, Motor.Scooter, Motor.Turbine
}), "Selection must put V8 profiles first, followed by the smaller piston engines and turbine.");
foreach (var profile in EngineProfile.All)
{
    int restored = EngineProfile.DisplayIndexForMotorId((int)profile.Motor);
    Check(EngineProfile.DisplayOrder[restored].Motor == profile.Motor,
        "Existing saved motor IDs must survive the changed display order.");
}
Check(EngineProfile.DisplayIndexForMotorId(-1) == 0 && EngineProfile.DisplayIndexForMotorId(999) == 0,
    "Invalid saved selections must fall back to the first V8.");
Console.WriteLine("PASS display order and saved motor selection compatibility");

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

foreach (var profile in EngineProfile.All)
{
    var engine = new EngineSimulator();
    EngineSnapshot idle = engine.Tick(0, profile);
    EngineSnapshot revved = idle;
    // Pedal must work without any GPS fixes, while keeping the measured speed zero.
    for (int i = 1; i <= 400; i++) revved = engine.Tick(i * .02, profile, 1);
    Check(revved.Rpm > profile.MaxRpm * .97, "Full pedal must reach maximum RPM in standstill.");
    Check(revved.SpeedKmh == 0 && revved.SignalLost && revved.Gear == 0,
        "Pedal must not fabricate GPS speed, signal or a driving gear.");
    Check(revved.State == DrivingState.Revving && revved.Load > .99, "Pedal must apply engine load.");
    Check(engine.Tick(8.02, profile, 4).Rpm <= profile.MaxRpm, "Pedal must clamp excessive input.");
    for (int i = 402; i <= 1000; i++) revved = engine.Tick(i * .02, profile, 0);
    Check(revved.State == DrivingState.Idle && revved.Rpm < profile.IdleRpm * 1.02,
        "Releasing the pedal must return to idle, including turbine spool-down.");
    Check(engine.Tick(20.02, profile, double.NaN).State == DrivingState.Idle, "Invalid pedal input must be released.");
    // Pedal release while moving must hand control back to real speed.
    for (int i = 1002; i <= 1400; i++)
    {
        double t = i * .02;
        if (i % 5 == 0) engine.PushSpeed(3, 5, t, t);
        revved = engine.Tick(t, profile, i < 1200 ? 1 : 0);
    }
    Check(revved.SpeedKmh > 10 && !revved.SignalLost && revved.State != DrivingState.Revving,
        "GPS control must resume after releasing the pedal while moving.");
    Console.WriteLine($"PASS {profile.Name}: stationary pedal, release and GPS handover");
}

static double Rms(IEnumerable<short> samples) => Math.Sqrt(samples.Average(x => (double)x * x));

static short[] RenderSteady(double rpm, double load, int rate = 44100, Motor motor = Motor.V8)
{
    var synth = new Synthesizer();
    var snapshot = new EngineSnapshot(10, 0, rpm, load, 2, DrivingState.Cruising, false);
    var samples = new short[rate];
    synth.Render(samples, snapshot, motor, 1, rate); // warm up smoothing/filters
    synth.Render(samples, snapshot, motor, 1, rate);
    return samples;
}

foreach (int rate in new[] { 44100, 48000 })
{
    foreach (var profile in EngineProfile.All)
    {
        foreach (double rev in new[] { profile.IdleRpm, (profile.IdleRpm + profile.MaxRpm) / 2, profile.MaxRpm })
        {
            var pcm = RenderSteady(rev, 1, rate, profile.Motor);
            int peak = pcm.Max(x => Math.Abs((int)x));
            Check(peak <= short.MaxValue * .951, "Full-load output must stay below the limiter ceiling.");
            Check(peak > short.MaxValue * .75, $"{profile.Name}: full throttle must use the PCM range.");
            Check(Rms(pcm) > short.MaxValue * .23, $"{profile.Name}: full throttle must produce a strong sustained level.");
            // A quieter slider setting must stay quieter rather than being undone by automatic gain.
            var synth = new Synthesizer();
            var snapshot = new EngineSnapshot(0, 0, rev, 1, 0, DrivingState.Revving, false);
            synth.Render(pcm, snapshot, profile.Motor, .25, rate);
            synth.Render(pcm, snapshot, profile.Motor, .25, rate);
            Check(pcm.Max(x => Math.Abs((int)x)) < short.MaxValue * .24,
                "User volume must be applied after the peak controller.");
        }
    }
    Console.WriteLine($"PASS {rate} Hz: all profiles loud at full load, bounded peaks and effective volume slider");
    foreach (var profile in EngineProfile.All)
    {
        var idle = new EngineSnapshot(0, 0, profile.IdleRpm, .08, 1, DrivingState.Idle, false);
        var whole = new Synthesizer();
        var blocks = new Synthesizer();
        whole.Start(); blocks.Start();
        short[] continuous = new short[rate * 3], chunked = new short[continuous.Length];
        whole.Render(continuous, idle, profile.Motor, .8, rate);
        for (int offset = 0; offset < chunked.Length; offset += 317)
            blocks.Render(chunked.AsSpan(offset, Math.Min(317, chunked.Length - offset)), idle, profile.Motor, .8, rate);
        Check(continuous.SequenceEqual(chunked), "Startup/phase must not depend on PCM block size.");
        Check(!whole.IsStarting && !blocks.IsStarting, "Starter must finish and hand over to idle.");
        Check(Rms(continuous.Skip(rate / 10).Take(rate / 4)) > 80, "Cranking must be audible before ignition.");
        Check(Rms(continuous.Skip(rate * 2)) > 300, "Ignition must settle into audible idle.");
        Check(continuous.Max(x => Math.Abs((int)x)) < short.MaxValue * .9, "Startup must retain headroom.");
        int largestStep = continuous.Zip(continuous.Skip(1), (a, b) => Math.Abs(a - b)).Max();
        Check(largestStep < 12000, "Startup must not click at ignition or idle handover.");
        whole.Render(continuous, idle, profile.Motor, 0, rate);
        Check(continuous.TakeLast(rate).All(x => x == 0), "Muted starter/idle must settle to silence.");
    }
    var quiet = RenderSteady(1600, .08, rate);
    var throttle = RenderSteady(1600, .85, rate);
    Check(Rms(throttle) > Rms(quiet) * 1.4, "Throttle must add body at the same RPM.");
    Console.WriteLine($"PASS {rate} Hz: all motor starts, PCM block continuity, mute and V8 throttle dynamics");
}

// Hann-windowed FFT measures actual audible bass (20–220 Hz) versus
// upper harmonics above 900 Hz, without leakage from a shallow test filter.
static Complex[] Spectrum(short[] samples, int n = 32768, int sampleOffset = 0)
{
    var bins = new Complex[n];
    for (int i = 0; i < n; i++)
        bins[i] = samples[sampleOffset + i] * (.5 - .5 * Math.Cos(Math.Tau * i / (n - 1)));
    for (int i = 1, j = 0; i < n; i++)
    {
        int bit = n >> 1;
        for (; (j & bit) != 0; bit >>= 1) j ^= bit;
        j ^= bit;
        if (i < j) (bins[i], bins[j]) = (bins[j], bins[i]);
    }
    for (int length = 2; length <= n; length <<= 1)
    {
        Complex step = Complex.FromPolarCoordinates(1, -Math.Tau / length);
        for (int offset = 0; offset < n; offset += length)
        {
            Complex w = Complex.One;
            for (int k = 0; k < length / 2; k++, w *= step)
            {
                Complex even = bins[offset + k], odd = w * bins[offset + k + length / 2];
                bins[offset + k] = even + odd;
                bins[offset + k + length / 2] = even - odd;
            }
        }
    }
    return bins;
}

static double BassTrebleRatio(short[] samples, int rate)
{
    var bins = Spectrum(samples);
    int n = bins.Length;
    double bass = 0, treble = 0;
    for (int i = 1; i < n / 2; i++)
    {
        double hz = (double)i * rate / n;
        double energy = bins[i].Magnitude * bins[i].Magnitude;
        if (hz is >= 20 and <= 220) bass += energy;
        if (hz >= 900) treble += energy;
    }
    return Math.Sqrt(bass / treble);
}

foreach (int rate in new[] { 44100, 48000 })
{
    foreach (Motor type in new[] { Motor.V8, Motor.PlymouthV8, Motor.Diesel })
    {
        var profile = EngineProfile.All.Single(p => p.Motor == type);
        var synth = new Synthesizer();
        synth.Start();
        var pcm = new short[rate * 2];
        synth.Render(pcm, new EngineSnapshot(0, 0, profile.IdleRpm, .08, 1, DrivingState.Idle, true), type, 1, rate);
        // Analyze cranking before ignition: the old turbine oscillator made
        // this interval dominated by a narrow tone around 750 Hz.
        var bins = Spectrum(pcm, 8192, rate / 10);
        double total = 0, high = 0;
        for (int i = 1; i < bins.Length / 2; i++)
        {
            double hz = (double)i * rate / bins.Length;
            double power = bins[i].Magnitude * bins[i].Magnitude;
            if (hz is >= 30 and <= 5000) total += power;
            if (hz is >= 650 and <= 1300) high += power;
        }
        Check(high / total < .08, $"{type}: starter must not produce a high electrical whine ({high / total:P1}).");
        Check(Rms(pcm.Skip(rate / 10).Take(rate / 4)) > short.MaxValue * .03, "Low starter must remain audible.");
        Console.WriteLine($"PASS {rate} Hz {type}: starter high-band energy {high / total:P1}");
    }
}

foreach (double revs in new[] { 620d, 1600, 2800, 4400 })
{
    var samples = RenderSteady(revs, .7);
    double ratio = BassTrebleRatio(samples, 44100);
    Check(ratio > 4, $"V8 must stay bass-dominant, including at high RPM ({revs}: {ratio:F1}).");
    Check(samples.Max(x => Math.Abs((int)x)) < short.MaxValue * .9, "V8 must retain headroom at full volume.");
    Check(Math.Abs(samples.Average(x => (double)x)) < 150, "Exhaust must not contain a DC offset.");
    Console.WriteLine($"PASS V8 {revs} rpm: bass/treble RMS {ratio:F1}, peak {samples.Max(x => Math.Abs((int)x)) / (double)short.MaxValue:P0}");
}

foreach (int rate in new[] { 44100, 48000 })
{
    foreach (double rpm in new[] { 650d, 1800, 3200 })
    {
        var pcm = RenderSteady(rpm, .8, rate, Motor.Diesel);
        double ratio = BassTrebleRatio(pcm, rate);
        Check(ratio > 10, $"Diesel must retain body rather than continuous hiss ({rpm} rpm: {ratio:F1}).");
        Check(Math.Abs(pcm.Average(x => (double)x)) < 200, "Diesel must not develop a DC offset.");
        var spectrum = Spectrum(pcm);
        int peak = Enumerable.Range(1, spectrum.Length / 2 - 1)
            .Where(i => i * (double)rate / spectrum.Length < 500)
            .MaxBy(i => spectrum[i].Magnitude);
        double peakHz = peak * (double)rate / spectrum.Length;
        Check(Math.Abs(peakHz - rpm / 60 * 3) < rate / (double)spectrum.Length * 2,
            "Diesel firing frequency must follow a four-stroke six-cylinder engine.");
        Console.WriteLine($"PASS {rate} Hz Diesel {rpm} rpm: bass/treble {ratio:F1}, firing peak {peakHz:F1} Hz");
    }
}

var cruise = new EngineSimulator();
EngineSnapshot cruiseSnapshot = cruise.Tick(0, motor);
for (int i = 1; i <= 400; i++)
{
    double t = i * .1;
    cruise.PushSpeed(Math.Min(25 / 3.6, t * .4), 5, t, t);
    cruiseSnapshot = cruise.Tick(t, motor);
}
Check(cruiseSnapshot.Gear == 5 && cruiseSnapshot.Rpm < 2800, "V8 steady cruising must use low revs in top gear.");
// A long gap represents missing fixes with the screen locked, followed by recovery.
for (int i = 401; i <= 500; i++) cruiseSnapshot = cruise.Tick(i * .1, motor);
Check(cruiseSnapshot.SignalLost, "GPS outage must be reported.");
Check(cruise.PushSpeed(4, 5, 50.1, 50.1), "A fresh GPS fix must recover after a long outage.");
Check(!cruise.Tick(50.1, motor).SignalLost, "GPS recovery must resume the engine without a UI tick.");
Console.WriteLine("PASS low-rev V8 cruise and GPS outage/recovery");

// Exercise several actual loop wraps at both native output rates. A discontinuity
// at the embedded loop seam would otherwise repeat as a click on every ride.
foreach (int rate in new[] { 44100, 48000 })
{
    var plymouth = EngineProfile.All.Single(p => p.Motor == Motor.PlymouthV8);
    foreach (double rpm in new[] { plymouth.IdleRpm, 1800, plymouth.MaxRpm })
    {
        var snapshot = new EngineSnapshot(15, 0, rpm, .8, 2, DrivingState.Cruising, false);
        var synth = new Synthesizer();
        var samples = new short[rate * 16];
        synth.Render(samples, snapshot, Motor.PlymouthV8, 1, rate);
        Check(samples.Max(x => Math.Abs((int)x)) < short.MaxValue * .9, "Plymouth must not clip at full volume.");
        int largestStep = samples.Zip(samples.Skip(1), (a, b) => Math.Abs(a - b)).Max();
        Check(largestStep < 5000, $"Plymouth loop must not click at {rpm} rpm ({largestStep}).");
        Check(Math.Abs(samples.Average(x => (double)x)) < 200, "Plymouth must not develop a DC offset.");
        var steady = RenderSteady(rpm, .8, rate, Motor.PlymouthV8);
        double ratio = BassTrebleRatio(steady, rate);
        Check(ratio > 4, $"Plymouth must preserve deep exhaust at {rpm} rpm/{rate} Hz (bass/treble {ratio:F1}).");
    }
    var quiet = RenderSteady(1600, .08, rate, Motor.PlymouthV8);
    var throttle = RenderSteady(1600, .85, rate, Motor.PlymouthV8);
    Check(Rms(throttle) > Rms(quiet) * 1.4, "Plymouth throttle must add body.");
    Check(!quiet.SequenceEqual(RenderSteady(1600, .08, rate)), "Plymouth must have its own sound.");
    Console.WriteLine($"PASS {rate} Hz: Plymouth loop seams, bass, throttle and headroom");
}

if (args.Length == 2 && (args[0] is "--preview" or "--preview-plymouth" or "--preview-pedal" or "--preview-diesel"))
{
    var previewProfile = args[0] switch
    {
        "--preview-plymouth" => EngineProfile.All.Single(p => p.Motor == Motor.PlymouthV8),
        "--preview-diesel" => EngineProfile.All.Single(p => p.Motor == Motor.Diesel),
        _ => motor
    };
    string path = Path.GetFullPath(args[1]);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var writer = new BinaryWriter(File.Create(path));
    const int rate = 44100, seconds = 22;
    writer.Write("RIFF"u8); writer.Write(36 + rate * seconds * 2); writer.Write("WAVEfmt "u8);
    writer.Write(16); writer.Write((short)1); writer.Write((short)1);
    writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
    writer.Write("data"u8); writer.Write(rate * seconds * 2);
    var engine = new EngineSimulator();
    var synth = new Synthesizer();
    synth.Start();
    short[] block = new short[882];
    for (int i = 0; i < seconds * 50; i++)
    {
        double t = i / 50d;
        double kmh = t < 3 ? 0 : t < 11 ? (t - 3) / 8 * 25 : t < 14 ? 25 : Math.Max(0, 25 - (t - 14) * 5);
        bool pedalDemo = args[0] is "--preview-pedal" or "--preview-diesel";
        if (i % 5 == 0 && !pedalDemo) engine.PushSpeed(kmh / 3.6, 5, t, t);
        double pedal = pedalDemo ? t is >= 4 and < 6 ? .4 : t is >= 9 and < 12 ? 1 : t is >= 15 and < 17 ? .7 : 0 : 0;
        synth.Render(block, engine.Tick(t, previewProfile, pedal), previewProfile.Motor, 1);
        foreach (short value in block) writer.Write(value);
    }
    Console.WriteLine($"{previewProfile.Name} preview: {path}");
}
