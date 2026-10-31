namespace ScooterSound.Core;

// Piston engines only. No look-ahead, allocations or file access in the audio callback.
internal sealed class PistonOutputStage
{
    public const double Ceiling = .9440608762859234; // -0.5 dBFS
    double input, dc, energy, compressorGain = 1, limiterGain = 1;
    double boostDb = 6, drive, previous, driveDc, driveInput;
    double finalInput, finalDc, dcServo;
    Biquad antiAlias1 = new(.541196100146197), antiAlias2 = new(1.306562964876377);

    public void Reset()
    {
        input = dc = energy = previous = driveDc = driveInput = drive = 0;
        finalInput = finalDc = dcServo = 0;
        compressorGain = limiterGain = 1;
        boostDb = 6;
        antiAlias1.Reset(); antiAlias2.Reset();
    }

    public double Process(double sample, Motor motor, AudioSettings settings, double dt)
    {
        double smoothing = 1 - Math.Exp(-dt / .05);
        boostDb += smoothing * (settings.BoostDb - boostDb);
        drive += smoothing * (settings.Overdrive - drive);
        // Remove unusable sub-bass before giving the loudspeaker useful headroom.
        dc = sample - input + Math.Exp(-Math.Tau * 28 * dt) * dc;
        input = sample;
        double trim = motor switch
        {
            Motor.V8 => 3.6, Motor.PlymouthV8 => 3.2,
            Motor.Diesel => 3.4, Motor.Motorcycle => 3.4, _ => 3.8
        };
        double body = dc * trim;
        energy += (1 - Math.Exp(-dt / .025)) * (body * body - energy);
        double rms = Math.Sqrt(Math.Max(0, energy));
        // 4:1 feed-forward RMS compression above -10 dBFS, before the user's boost.
        double reduction = rms > .316227766 ? Math.Pow(.316227766 / rms, .75) : 1;
        double response = reduction < compressorGain ? .005 : .12;
        compressorGain += (1 - Math.Exp(-dt / response)) * (reduction - compressorGain);
        double boosted = body * compressorGain * Math.Pow(10, boostDb / 20);

        // Four-times oversampled soft overdrive. Fourth-order Butterworth low-pass
        // removes high-frequency products before decimation. Dry path is bit-exact
        // when the control has reached zero; the filters stay warm for live changes.
        double wet = 0;
        double amount = 1 + 8 * drive;
        for (int sub = 1; sub <= 4; sub++)
        {
            double upsampled = previous + (boosted - previous) * (sub / 4d);
            double shaped = drive > 1e-8 ? Math.Tanh(upsampled * amount) / Math.Tanh(amount) : upsampled;
            wet = antiAlias2.Process(antiAlias1.Process(shaped));
        }
        previous = boosted;
        double driven = drive < 1e-8 ? boosted : boosted + drive * (wet - boosted);
        // Symmetric saturation can still introduce DC with asymmetric exhaust pulses.
        driveDc = driven - driveInput + Math.Exp(-Math.Tau * 18 * dt) * driveDc;
        driveInput = driven;
        // Leave room for the final DC correction, rather than clipping its offset away.
        const double workingCeiling = .90;
        double wanted = Math.Min(1, workingCeiling / Math.Max(workingCeiling, Math.Abs(driveDc)));
        limiterGain = wanted < limiterGain ? wanted
            : limiterGain + (1 - Math.Exp(-dt / .0015)) * (wanted - limiterGain);
        double limited = driveDc * limiterGain;
        // Gain reduction on asymmetric exhaust pulses must not leave a DC bias.
        finalDc = limited - finalInput + Math.Exp(-Math.Tau * 1 * dt) * finalDc;
        finalInput = limited;
        double result = Math.Clamp(finalDc - dcServo, -Ceiling, Ceiling);
        // Servo follows the actual bounded output: the last ceiling cannot
        // reintroduce a sustained bias by shaving unequal exhaust peaks.
        dcServo += (1 - Math.Exp(-Math.Tau * dt)) * result;
        return result;
    }

    struct Biquad
    {
        readonly double b0, b1, b2, a1, a2;
        double z1, z2;
        public Biquad(double q)
        {
            // 0.85 * original Nyquist, at a four-times sample rate.
            double w = Math.Tau * .10625;
            double cos = Math.Cos(w), alpha = Math.Sin(w) / (2 * q), norm = 1 + alpha;
            b0 = (1 - cos) / (2 * norm); b1 = (1 - cos) / norm; b2 = b0;
            a1 = -2 * cos / norm; a2 = (1 - alpha) / norm;
            z1 = z2 = 0;
        }
        public double Process(double value)
        {
            double result = value * b0 + z1;
            z1 = value * b1 - a1 * result + z2;
            z2 = value * b2 - a2 * result;
            return result;
        }
        public void Reset() => z1 = z2 = 0;
    }
}
