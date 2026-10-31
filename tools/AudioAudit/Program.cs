using ScooterSound.Core;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length != 2) throw new ArgumentException("Usage: AudioAudit <report.json> <wav-directory>");
string wavDirectory = Path.GetFullPath(args[1]);
Directory.CreateDirectory(wavDirectory);
var results = new List<Measurement>();
foreach (int rate in new[] { 44100, 48000 })
foreach (var profile in EngineProfile.All)
{
    foreach (string scenario in new[] { "idle", "cruise", "load", "ride" })
    {
        bool ride = scenario == "ride";
        var pcm = AudioScenarios.Render(profile.Motor, rate, scenario);
        short[] measured = ride ? pcm : pcm[(rate * 2)..];
        byte[] bytes = new byte[pcm.Length * 2];
        Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
        double rms = Math.Sqrt(measured.Average(v => (double)v * v)) / short.MaxValue;
        results.Add(new(profile.Motor.ToString(), rate, scenario, Convert.ToHexString(SHA256.HashData(bytes)), 20 * Math.Log10(rms), measured.Max(v => Math.Abs((int)v)) / (double)short.MaxValue, measured.Average(v => (double)v)));
        if (ride && rate == 44100)
        {
            WriteWav(Path.Combine(wavDirectory, $"{profile.Motor}.wav"), pcm, rate);
            if (profile.Motor is Motor.V8 or Motor.PlymouthV8)
                WriteWav(Path.Combine(wavDirectory, $"{profile.Motor}-overdrive.wav"),
                    AudioScenarios.Render(profile.Motor, rate, "ride", new AudioSettings(1, 6, 1)), rate);
        }
    }
}
File.WriteAllText(args[0], JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
foreach (var r in results.Where(r => r.Rate == 44100 && r.Scenario != "ride"))
    Console.WriteLine($"{r.Motor,-12} {r.Scenario,-6} RMS {r.RmsDb,6:F2} dBFS, peak {r.Peak:F3}, mean {r.Mean:F1}");
static void WriteWav(string path, short[] pcm, int rate)
{
    using var writer = new BinaryWriter(File.Create(path));
    writer.Write("RIFF"u8); writer.Write(36 + pcm.Length * 2); writer.Write("WAVEfmt "u8);
    writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(rate);
    writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
    writer.Write("data"u8); writer.Write(pcm.Length * 2);
    foreach (short sample in pcm) writer.Write(sample);
}
record Measurement(string Motor, int Rate, string Scenario, string Sha256, double RmsDb, double Peak, double Mean);
