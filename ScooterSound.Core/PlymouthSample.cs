namespace ScooterSound.Core;

// Short, seamless mono exhaust loop derived from the supplied reference.
// Decode once; the audio callback only interpolates samples, without file I/O.
internal static class PlymouthSample
{
    public const int SampleRate = 11025;
    // Playback tuning reference, not a tachometer measurement of the recording.
    public const double ReferenceRpm = 1100;
    public static readonly short[] Pcm = Load();

    static short[] Load()
    {
        using var stream = typeof(PlymouthSample).Assembly.GetManifestResourceStream("ScooterSound.Core.PlymouthIdle.pcm")
            ?? throw new InvalidOperationException("Plymouth exhaust sample is missing.");
        using var reader = new BinaryReader(stream);
        var pcm = new short[checked((int)(stream.Length / sizeof(short)))];
        for (int i = 0; i < pcm.Length; i++) pcm[i] = reader.ReadInt16();
        return pcm;
    }
}
