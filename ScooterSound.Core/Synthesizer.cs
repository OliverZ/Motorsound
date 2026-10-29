namespace ScooterSound.Core;

// Continuous, allocation-free PCM synthesis. All phases/filters survive render blocks.
public sealed class Synthesizer
{
    static Synthesizer() { _ = PlymouthSample.Pcm; }

    double phase, whine, crank, filteredNoise, exhaust, exhaustBody, exhaustTail, dcInput, dcOutput;
    double smoothRpm = 800, smoothLoad, smoothVolume;
    double startTime = double.PositiveInfinity;
    double plymouthPosition, plymouthExhaust, plymouthTail, plymouthDcInput, plymouthDcOutput;
    uint random = 0x12345678;
    public bool IsStarting => startTime < 1.65;

    // Called once for a new ride, not for each GPS update or motor selection.
    public void Start()
    {
        phase = whine = crank = filteredNoise = exhaust = exhaustBody = exhaustTail = dcInput = dcOutput = 0;
        smoothRpm = 180;
        smoothLoad = smoothVolume = 0;
        startTime = 0;
        plymouthPosition = plymouthExhaust = plymouthTail = plymouthDcInput = plymouthDcOutput = 0;
        random = 0x12345678;
    }

    public void Render(Span<short> output, EngineSnapshot snapshot, Motor motor, double volume, int sampleRate = 44100)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
        double dt = 1d / sampleRate;
        double smoothing = 1 - Math.Exp(-dt / .045);
        double noiseFilter = 1 - Math.Exp(-Math.Tau * 850 * dt);
        var profile = EngineProfile.All[(int)motor];
        for (int i = 0; i < output.Length; i++)
        {
            bool starting = IsStarting;
            double ignition = starting ? SmoothStep((startTime - .48) / .30) : 1;
            double targetRpm = snapshot.Rpm;
            if (starting)
            {
                // Compression strokes, ignition flare, then settle onto the idle bed.
                double catchUp = SmoothStep((startTime - .48) / .25);
                double flare = Math.Exp(-Math.Max(0, startTime - .73) / .23);
                targetRpm = 180 + catchUp * (profile.IdleRpm - 180 + profile.IdleRpm * .8 * flare);
                // Blend into live driving only after the engine has caught.
                double drive = SmoothStep((startTime - 1.2) / .45);
                targetRpm += drive * (snapshot.Rpm - profile.IdleRpm);
            }
            smoothRpm += smoothing * (targetRpm - smoothRpm);
            smoothLoad += smoothing * ((starting ? .12 : snapshot.Load) - smoothLoad);
            smoothVolume += smoothing * (Math.Clamp(volume, 0, 1) - smoothVolume);
            double pulses = motor switch { Motor.V8 or Motor.PlymouthV8 => 4, Motor.Motorcycle => 2, _ => 1 };
            phase = (phase + smoothRpm / 60 * pulses * dt) % 1;
            // One complete four-stroke cycle (two crank revolutions), eight V8 firings.
            crank = (crank + smoothRpm / 120 * dt) % 1;
            whine = (whine + (700 + smoothRpm * .28) * dt) % 1;
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            double noise = random / (double)uint.MaxValue * 2 - 1;
            filteredNoise += noiseFilter * (noise - filteredNoise);
            double p = phase * Math.Tau;
            double sound = motor switch
            {
                Motor.Scooter => .5 * Math.Sin(p) + .25 * Math.Sin(3 * p) + .12 * Math.Sin(7 * p) + .12 * noise * smoothLoad,
                Motor.Motorcycle => .55 * Math.Sin(p) + .24 * Math.Sin(2 * p) + .14 * Math.Sin(4 * p) + .08 * filteredNoise,
                Motor.V8 => V8(dt),
                Motor.PlymouthV8 => Plymouth(dt),
                Motor.Turbine => .3 * Math.Sin(whine * Math.Tau) + (.35 + .4 * smoothLoad) * filteredNoise + .08 * Math.Sin(p),
                _ => .5 * Math.Sin(p) + .22 * Math.Sin(2 * p) + .3 * Math.Pow(Math.Max(0, Math.Sin(p)), 12) + .15 * noise * smoothLoad
            };
            double gain = motor switch
            {
                Motor.V8 => .32 + .28 * smoothLoad,
                Motor.PlymouthV8 => .40 + .38 * smoothLoad,
                _ => .18 + .24 * smoothLoad
            };
            double starter = 0;
            if (starting)
            {
                double envelope = SmoothStep(startTime / .06) * (1 - SmoothStep((startTime - .48) / .28));
                double compression = .55 + .45 * Math.Cos(crank * Math.Tau * 8);
                starter = envelope * (.14 * Math.Sin(whine * Math.Tau) + .20 * filteredNoise) * compression;
                startTime += dt;
            }
            double sample = (sound * gain * ignition + starter) * smoothVolume;
            output[i] = (short)(Math.Clamp(sample, -.95, .95) * short.MaxValue);
        }
    }

    double Plymouth(double dt)
    {
        var pcm = PlymouthSample.Pcm;
        int index = (int)plymouthPosition;
        int next = index + 1 == pcm.Length ? 0 : index + 1;
        double fraction = plymouthPosition - index;
        double sample = (pcm[index] + (pcm[next] - pcm[index]) * fraction) / short.MaxValue;
        // GPS changes playback speed continuously. The multi-cycle recording
        // supplies the irregular burble; throttle adds compression and body.
        plymouthPosition = (plymouthPosition + PlymouthSample.SampleRate * dt * smoothRpm / PlymouthSample.ReferenceRpm) % pcm.Length;
        double body = .12 * Math.Sin(crank * Math.Tau * 4) + .08 * Math.Sin(crank * Math.Tau * 6 + .4);
        double saturated = .85 * Math.Tanh((sample + body) * (1.2 + .7 * smoothLoad));
        // Pitching up the recording also raises its upper harmonics. Keep the
        // exhaust dark as revs rise and remove any DC introduced by saturation.
        double alpha = 1 - Math.Exp(-Math.Tau * (450 + 250 * smoothLoad) * dt);
        plymouthExhaust += alpha * (saturated - plymouthExhaust);
        plymouthTail += alpha * (plymouthExhaust - plymouthTail);
        plymouthDcOutput = plymouthTail - plymouthDcInput + Math.Exp(-Math.Tau * 20 * dt) * plymouthDcOutput;
        plymouthDcInput = plymouthTail;
        return plymouthDcOutput;
    }

    double V8(double dt)
    {
        // Crossplane firing order 1-8-4-3-6-5-7-2: each bank has uneven
        // 90/180/270-degree gaps. Separate banks retain the characteristic burble
        // instead of collapsing eight firings into a high, uniform sine tone.
        double left = Pulse(crank) + Pulse(crank - 3d / 8) + Pulse(crank - 5d / 8) + Pulse(crank - 6d / 8);
        double right = Pulse(crank - 1d / 8) + Pulse(crank - 2d / 8) + Pulse(crank - 4d / 8) + Pulse(crank - 7d / 8);
        // Subtract the analytic pulse mean, then mix unequal exhaust paths.
        const double bankMean = 4 * (1d / 18 - 1d / 110);
        double banks = (left - bankMean) * .95 + (right - bankMean) * .45;
        double cycle = crank * Math.Tau;
        double rumble = .38 * Math.Sin(cycle * 4) + .18 * Math.Sin(cycle * 6 + .4);
        double firing = .24 * Math.Sin(cycle * 8) + .045 * Math.Sin(cycle * 16);
        double raw = banks * 1.9 + rumble + firing + .035 * filteredNoise * smoothLoad;
        // Two exhaust low-pass poles: throttle adds growl, never a bright hiss.
        double cutoff = 260 + 240 * smoothLoad;
        double alpha = 1 - Math.Exp(-Math.Tau * cutoff * dt);
        exhaust += alpha * (raw - exhaust);
        exhaustBody += alpha * (exhaust - exhaustBody);
        double saturated = .85 * Math.Tanh(exhaustBody * (1.35 + .4 * smoothLoad));
        // Muffle the upper harmonics introduced by saturation as well.
        exhaustTail += (1 - Math.Exp(-Math.Tau * 450 * dt)) * (saturated - exhaustTail);
        // Remove DC and infrasonic energy so headroom goes into audible bass.
        dcOutput = exhaustTail - dcInput + Math.Exp(-Math.Tau * 20 * dt) * dcOutput;
        dcInput = exhaustTail;
        return dcOutput;
    }

    static double Pulse(double position)
    {
        position -= Math.Floor(position);
        // Soft attack avoids the broadband click of a sawtooth exhaust pulse.
        return Math.Exp(-position * 18) - Math.Exp(-position * 110);
    }

    static double SmoothStep(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }
}
