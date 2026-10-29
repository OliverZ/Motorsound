namespace ScooterSound.Core;

// Append profiles to preserve saved motor indices from earlier versions.
public enum Motor { Scooter, Motorcycle, V8, Turbine, Diesel, PlymouthV8 }
public enum DrivingState { Idle, Accelerating, Cruising, Decelerating, Revving }
public sealed record EngineProfile(Motor Motor, string Name, string Description, double IdleRpm, double MaxRpm)
{
    public static IReadOnlyList<EngineProfile> All { get; } = new[]
    {
        new EngineProfile(Motor.Scooter, "50-ccm-Roller", "Zweitakt · Variomatik", 1600, 8500),
        new EngineProfile(Motor.Motorcycle, "Motorrad", "Vierzylinder · 6 Gänge", 1200, 12000),
        new EngineProfile(Motor.V8, "V8-Auto", "Tiefer Crossplane-V8 · 5 Gänge", 620, 4400),
        new EngineProfile(Motor.Turbine, "Flugzeugturbine", "Strahltriebwerk · sanftes Hochlaufen", 2500, 18000),
        new EngineProfile(Motor.Diesel, "Diesel", "Selbstzünder · 5 Gänge", 800, 4200),
        new EngineProfile(Motor.PlymouthV8, "Plymouth V8", "US-V8 · Auspuffaufnahme · 3 Gänge", 680, 4000)
    };
}

public sealed record EngineSnapshot(double SpeedKmh, double Acceleration, double Rpm,
    double Load, int Gear, DrivingState State, bool SignalLost);

// All times are monotonic seconds, never wall-clock timestamps.
public sealed class EngineSimulator
{
    double speed, acceleration, lastSample = double.NegativeInfinity, lastTick = double.NaN;
    double rpm;
    int gear = 1;
    Motor? previousMotor;
    DrivingState state;
    public double RoadRangeKmh { get; set; } = 25;

    public bool PushSpeed(double metresPerSecond, double accuracyMetres, double sampleTime, double now)
    {
        if (!double.IsFinite(metresPerSecond) || metresPerSecond < 0 || metresPerSecond > 35 ||
            !double.IsFinite(accuracyMetres) || accuracyMetres < 0 || accuracyMetres > 40 ||
            !double.IsFinite(sampleTime) || !double.IsFinite(now) ||
            now - sampleTime > 3 || sampleTime > now + .5 || sampleTime <= lastSample)
            return false;
        double dt = double.IsFinite(lastSample) ? sampleTime - lastSample : 1;
        if (dt < .05) return false;
        if (double.IsFinite(lastSample) && dt < 3 && Math.Abs(metresPerSecond - speed) / dt > 8)
            return false;
        double old = speed;
        double alpha = 1 - Math.Exp(-dt / .65);
        speed += alpha * (metresPerSecond - speed);
        if (speed < .25 && metresPerSecond < .25) speed = 0;
        acceleration += (1 - Math.Exp(-dt / .9)) * ((speed - old) / dt - acceleration);
        lastSample = sampleTime;
        return true;
    }

    public EngineSnapshot Tick(double now, EngineProfile profile, double throttle = 0)
    {
        double dt = double.IsNaN(lastTick) ? .02 : Math.Clamp(now - lastTick, 0, .25);
        lastTick = now;
        bool lost = now - lastSample > 3;
        if (lost)
        {
            speed *= Math.Exp(-dt / 1.2);
            acceleration *= Math.Exp(-dt / .5);
            if (speed < .1) speed = 0;
        }
        if (previousMotor != profile.Motor)
        {
            gear = 1;
            rpm = profile.IdleRpm;
            previousMotor = profile.Motor;
        }
        if (speed < .3) state = DrivingState.Idle;
        else if (acceleration > .25) state = DrivingState.Accelerating;
        else if (acceleration < -.3) state = DrivingState.Decelerating;
        else if (Math.Abs(acceleration) < .12) state = DrivingState.Cruising;
        else if (state == DrivingState.Revving) state = DrivingState.Cruising;
        double normalized = Math.Clamp(speed * 3.6 / Math.Max(5, RoadRangeKmh), 0, 1);
        double load = state == DrivingState.Idle ? .08 :
            Math.Clamp(.3 + acceleration * .5 + normalized * .15, .05, 1);
        double rev;
        if (profile.Motor == Motor.Turbine)
            rev = normalized * .8 + load * .15;
        else if (profile.Motor == Motor.Scooter)
            rev = speed < .3 ? 0 : Math.Clamp(.35 + normalized * .35 + load * .2, 0, 1);
        else
        {
            int gears = profile.Motor switch { Motor.Motorcycle => 6, Motor.PlymouthV8 => 3, _ => 5 };
            while (gear < gears && normalized > gear / (double)gears + .035) gear++;
            while (gear > 1 && normalized < (gear - 1) / (double)gears - .045) gear--;
            rev = speed < .3 ? 0 : Math.Clamp(.16 + normalized * gears / gear * .66 + load * .1, 0, 1);
            // A large-displacement V8 pulls from low revs; load opens the exhaust
            // and raises revs without turning a steady scooter ride into a race engine.
            if (profile.Motor is Motor.V8 or Motor.PlymouthV8 && speed >= .3)
                rev = Math.Clamp(.06 + normalized * gears / gear * (.30 + .16 * load) + .16 * load, 0, 1);
        }
        if (state == DrivingState.Idle) rev = 0;
        throttle = double.IsFinite(throttle) ? Math.Clamp(throttle, 0, 1) : 0;
        if (throttle > 0)
        {
            // Pedal controls the engine, never the measured GPS speed.
            rev = Math.Max(rev, throttle);
            load = Math.Max(load, .08 + .92 * throttle);
            if (speed < .3) state = DrivingState.Revving;
        }
        double target = profile.IdleRpm + rev * (profile.MaxRpm - profile.IdleRpm);
        double response = profile.Motor == Motor.Turbine ? 2 : target < rpm ? .55 : .18;
        rpm += (1 - Math.Exp(-dt / response)) * (target - rpm);
        return new(speed * 3.6, acceleration, rpm, load,
            state == DrivingState.Revving || profile.Motor is Motor.Scooter or Motor.Turbine ? 0 : gear, state, lost);
    }
}
