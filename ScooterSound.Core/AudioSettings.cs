namespace ScooterSound.Core;

/// <summary>One immutable snapshot shared by the UI and the audio renderer.</summary>
public sealed record AudioSettings(double Volume = 1, double BoostDb = 6, double Overdrive = 0)
{
    public static AudioSettings Default { get; } = new();
    public AudioSettings Normalize() => new(
        double.IsFinite(Volume) ? Math.Clamp(Volume, 0, 1) : 0,
        double.IsFinite(BoostDb) ? Math.Clamp(BoostDb, 0, 18) : 6,
        double.IsFinite(Overdrive) ? Math.Clamp(Overdrive, 0, 1) : 0);
}
