using ScooterSound.Core;
using System.Security.Cryptography;
using System.Text.Json;

internal static class AudioRegression
{
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
    static double Rms(short[] pcm) => Math.Sqrt(pcm.Average(x => (double)x * x)) / short.MaxValue;
    static string Hash(short[] pcm)
    {
        var bytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public static void Run()
    {
        using var stream = typeof(AudioRegression).Assembly.GetManifestResourceStream("AudioBaseline")!;
        using var baseline = JsonDocument.Parse(stream);
        foreach (var item in baseline.RootElement.EnumerateArray())
        {
            Motor motor = Enum.Parse<Motor>(item.GetProperty("Motor").GetString()!);
            int rate = item.GetProperty("Rate").GetInt32();
            string scenario = item.GetProperty("Scenario").GetString()!;
            if (motor == Motor.Turbine)
            {
                foreach (var settings in new[] { AudioSettings.Default, new AudioSettings(1, 0, 0), new AudioSettings(1, 18, 1) })
                    Check(Hash(AudioScenarios.Render(motor, rate, scenario, settings)) == item.GetProperty("Sha256").GetString(),
                        $"Turbine must remain sample-identical to v3.1 ({rate}/{scenario}, {settings}).");
                Console.WriteLine($"PASS unchanged turbine {rate} Hz {scenario}: v3.1 SHA-256 matches for all boost/drive settings");
            }
            else if (scenario is "idle" or "cruise")
            {
                var pcm = AudioScenarios.Render(motor, rate, scenario)[(rate * 2)..];
                double improvement = 20 * Math.Log10(Rms(pcm)) - item.GetProperty("RmsDb").GetDouble();
                Check(improvement >= (motor is Motor.V8 or Motor.PlymouthV8 ? 4 : .5),
                    $"{motor}/{scenario}/{rate}: insufficient loudness improvement ({improvement:F2} dB).");
                Console.WriteLine($"PASS {motor} {rate} Hz {scenario}: +{improvement:F2} dB RMS vs v3.1");
            }
        }

        foreach (int rate in new[] { 44100, 48000 })
        foreach (var profile in EngineProfile.All.Where(p => p.Motor != Motor.Turbine))
        {
            var snapshot = new EngineSnapshot(15, 0, 1600, .4, 2, DrivingState.Cruising, false);
            short[] Steady(AudioSettings settings)
            {
                var synth = new Synthesizer();
                var pcm = new short[rate];
                for (int i = 0; i < 3; i++) synth.Render(pcm, snapshot, profile.Motor, settings, rate);
                return pcm;
            }
            var dry = Steady(new AudioSettings(1, 0, 0));
            var boosted = Steady(AudioSettings.Default);
            var driven = Steady(new AudioSettings(1, 6, 1));
            Check(Rms(boosted) > Rms(dry) * 1.05, $"{profile.Name}: boost must remain effective after limiting.");
            Check(!boosted.SequenceEqual(driven), "Overdrive must change the sound separately from boost.");
            Check(Steady(new AudioSettings(1, 6, -1)).SequenceEqual(boosted), "Clamped zero overdrive must use the dry path.");
            var quiet = Steady(new AudioSettings(.25, 6, 0));
            Check(Math.Abs(Rms(quiet) / Rms(boosted) - .25) < .002, "Master volume must follow processing without being compensated.");
            foreach (double boost in new[] { 0d, 6, 18 })
            foreach (double drive in new[] { 0d, .5, 1 })
            {
                var pcm = Steady(new AudioSettings(1, boost, drive));
                Check(pcm.All(v => Math.Abs((int)v) <= short.MaxValue * .944061), "Peak ceiling exceeded.");
                Check(Rms(pcm) > .1, "Extreme settings must not unexpectedly mute the engine.");
                Check(Math.Abs(pcm.Average(x => (double)x)) < 350, "Processing must not introduce sustained DC.");
            }
            var whole = new Synthesizer(); var chunks = new Synthesizer();
            whole.Start(); chunks.Start();
            short[] continuous = new short[rate * 2], blocked = new short[rate * 2];
            var extremes = new AudioSettings(1, 18, 1);
            whole.Render(continuous, snapshot, profile.Motor, extremes, rate);
            for (int offset = 0; offset < blocked.Length; offset += 317)
                chunks.Render(blocked.AsSpan(offset, Math.Min(317, blocked.Length - offset)), snapshot, profile.Motor, extremes, rate);
            Check(continuous.SequenceEqual(blocked), "Compressor, oversampling and limiter must be independent of block size.");

            // Change controls while audio is running, including mute at maximum boost/drive.
            short[] previous = new short[rate / 10];
            var live = new Synthesizer();
            live.Render(previous, snapshot, profile.Motor, AudioSettings.Default, rate);
            foreach (var settings in new[] { extremes, new AudioSettings(1, 0, 0), new AudioSettings(0, 18, 1) })
            {
                short last = previous[^1];
                live.Render(previous, snapshot, profile.Motor, settings, rate);
                Check(Math.Abs(previous[0] - last) < 9000, "Live controls must not create a discontinuity.");
            }
            short[] tail = new short[rate * 2];
            live.Render(tail, snapshot, profile.Motor, new AudioSettings(0, 18, 1), rate);
            Check(tail.TakeLast(rate / 2).All(x => x == 0), "Mute must remain silent at maximum boost and drive.");
            Console.WriteLine($"PASS {profile.Name} {rate} Hz: independent controls, overdrive, peak bound, DC, live changes, mute and block continuity");
        }

        foreach (int rate in new[] { 44100, 48000 })
        {
            // Switch profiles and settings without restarting the audio callback.
            var synth = new Synthesizer();
            short[] pcm = new short[rate / 2];
            foreach (var profile in EngineProfile.DisplayOrder.Concat(EngineProfile.DisplayOrder.Reverse()))
            {
                synth.Render(pcm, new(10, 0, profile.IdleRpm, .4, 2, DrivingState.Cruising, false),
                    profile.Motor, new AudioSettings(1, 18, 1), rate);
                Check(pcm.All(v => Math.Abs((int)v) < short.MaxValue), "Motor switches must remain bounded.");
                Check(Rms(pcm) > .05, "Motor switches must retain audible output.");
            }
        }
    }
}
