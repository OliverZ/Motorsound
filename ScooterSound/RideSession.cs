using ScooterSound.Core;

namespace ScooterSound;

// Immutable snapshots cross the UI/service/audio thread boundary.
public static class RideSession
{
    static EngineSnapshot snapshot = new(0, 0, 800, 0, 0, DrivingState.Idle, true);
    static EngineProfile profile = EngineProfile.All[0];
    static double volume = Preferences.Default.Get("volume", .45);
    static double testSpeed;
    static string status = "Bereit";
    static int running;
    public static EngineSnapshot Snapshot { get => Volatile.Read(ref snapshot); set => Volatile.Write(ref snapshot, value); }
    public static EngineProfile Profile { get => Volatile.Read(ref profile); set => Volatile.Write(ref profile, value); }
    public static double Volume { get => Volatile.Read(ref volume); set => Volatile.Write(ref volume, value); }
    public static double TestSpeed { get => Volatile.Read(ref testSpeed); set => Volatile.Write(ref testSpeed, value); }
    public static string Status { get => Volatile.Read(ref status); set => Volatile.Write(ref status, value); }
    public static bool Running { get => Volatile.Read(ref running) == 1; set => Volatile.Write(ref running, value ? 1 : 0); }
}
