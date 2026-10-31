namespace ScooterSound.Core;

// Continuous, allocation-free PCM synthesis. All phases/filters survive render blocks.
public sealed class Synthesizer
{
    static Synthesizer() { _ = PlymouthSample.Pcm; }

    double phase, whine, crank, filteredNoise, exhaust, exhaustBody, exhaustTail, dcInput, dcOutput;
    double smoothRpm = 800, smoothLoad, smoothVolume;
    double startTime = double.PositiveInfinity;
    double plymouthPosition;
    double outputInput, outputDc, outputPeak, outputGain = 1;
    double starterRotor, starterNoise, starterBody;
    double dieselExhaust, dieselBody, dieselTail;
    uint random = 0x12345678;
    readonly PistonOutputStage pistonOutput = new();
    public bool IsStarting => startTime < 1.65;

    // Called once for a new ride, not for each GPS update or motor selection.
    public void Start()
    {
        phase = whine = crank = filteredNoise = exhaust = exhaustBody = exhaustTail = dcInput = dcOutput = 0;
        smoothRpm = 180;
        smoothLoad = smoothVolume = 0;
        startTime = 0;
        plymouthPosition = 0;
        outputInput = outputDc = outputPeak = 0;
        outputGain = 1;
        starterRotor = starterNoise = starterBody = 0;
        dieselExhaust = dieselBody = dieselTail = 0;
        random = 0x12345678;
        pistonOutput.Reset();
    }

    public void Render(Span<short> output, EngineSnapshot snapshot, Motor motor, double volume, int sampleRate = 44100)
        => Render(output, snapshot, motor, new AudioSettings(volume), sampleRate);

    public void Render(Span<short> output, EngineSnapshot snapshot, Motor motor, AudioSettings settings, int sampleRate = 44100)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sampleRate, 8000);
        ArgumentNullException.ThrowIfNull(settings);
        // Clamp once per block, without allocating on the Android render path.
        if (!double.IsFinite(settings.Volume) || settings.Volume is < 0 or > 1 ||
            !double.IsFinite(settings.BoostDb) || settings.BoostDb is < 0 or > 18 ||
            !double.IsFinite(settings.Overdrive) || settings.Overdrive is < 0 or > 1)
            settings = settings.Normalize();
        double dt = 1d / sampleRate;
        double smoothing = 1 - Math.Exp(-dt / .045);
        double volumeSmoothing = motor == Motor.Turbine ? smoothing : 1 - Math.Exp(-dt / .05);
        double noiseFilter = 1 - Math.Exp(-Math.Tau * 850 * dt);
        double dcFilter = Math.Exp(-Math.Tau * 18 * dt);
        double peakRelease = Math.Exp(-dt / .15);
        double gainRelease = 1 - Math.Exp(-dt / .12);
        double starterFilter = 1 - Math.Exp(-Math.Tau * 320 * dt);
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
            smoothVolume += volumeSmoothing * (Math.Clamp(settings.Volume, 0, 1) - smoothVolume);
            double pulses = motor switch { Motor.V8 or Motor.PlymouthV8 => 4, Motor.Diesel => 3, Motor.Motorcycle => 2, _ => 1 };
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
                Motor.Diesel => Diesel(dt),
                Motor.Turbine => .3 * Math.Sin(whine * Math.Tau) + (.35 + .4 * smoothLoad) * filteredNoise + .08 * Math.Sin(p),
                _ => throw new ArgumentOutOfRangeException(nameof(motor))
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
                if (motor is Motor.V8 or Motor.PlymouthV8 or Motor.Diesel)
                {
                    // V8/diesel starters should chug against compression, not share
                    // the turbine's clean ~750 Hz oscillator. Keep the gear tone
                    // subdued and let low, band-limited mechanical noise dominate.
                    starterRotor = (starterRotor + (155 + 9 * Math.Sin(startTime * 19)) * dt) % 1;
                    starterNoise += starterFilter * (noise - starterNoise);
                    starterBody += starterFilter * (starterNoise - starterBody);
                    double gears = .025 * Math.Sin(starterRotor * Math.Tau)
                        + .012 * Math.Sin(starterRotor * Math.Tau * 2);
                    int cylinders = motor == Motor.Diesel ? 6 : 8;
                    double strokes = .28 + .72 * Math.Pow(Math.Max(0, Math.Cos(crank * Math.Tau * cylinders)), 2);
                    starter = envelope * (1.05 * starterBody + gears) * strokes;
                }
                else starter = envelope * (.14 * Math.Sin(whine * Math.Tau) + .20 * filteredNoise) * compression;
                startTime += dt;
            }
            double raw = sound * gain * ignition + starter;
            if (motor != Motor.Turbine)
            {
                double pistonSample = pistonOutput.Process(raw, motor, settings, dt) * smoothVolume;
                output[i] = (short)(pistonSample * short.MaxValue);
                continue;
            }
            // Legacy turbine path: preserve every operation, including volume smoothing.
            // Use the available PCM range across all profiles. Peak tracking has
            // instant attack and a smooth release, so transients stay below the
            // ceiling without flattening the waveform through hard clipping.
            outputDc = raw - outputInput + dcFilter * outputDc;
            outputInput = raw;
            outputPeak = Math.Max(Math.Abs(outputDc), outputPeak * peakRelease);
            double ceiling = .55 + .40 * Math.Clamp(smoothLoad, 0, 1);
            double desiredGain = Math.Min(8, ceiling / Math.Max(.04, outputPeak));
            outputGain = desiredGain < outputGain ? desiredGain
                : outputGain + gainRelease * (desiredGain - outputGain);
            double sample = outputDc * outputGain * smoothVolume;
            output[i] = (short)(Math.Clamp(sample, -.98, .98) * short.MaxValue);
        }
    }

    double Diesel(double dt)
    {
        // Six cylinders, four strokes: three firings per revolution. Keep a
        // low exhaust body and short injection knocks instead of continuous hiss.
        double p = phase * Math.Tau;
        double pulse = Math.Exp(-phase * 10) - Math.Exp(-phase * 65);
        const double mean = 1d / 10 - 1d / 65;
        double body = .55 * Math.Sin(p) + .22 * Math.Sin(2 * p + .15) + .10 * Math.Sin(3 * p);
        double age = phase / Math.Max(1, smoothRpm / 60 * 3);
        double knockWindow = SmoothStep(phase / .02) * (1 - SmoothStep(phase / .35));
        double knock = Math.Sin(Math.Tau * 600 * age) * Math.Exp(-age / .0035) * knockWindow;
        double raw = body + .8 * (pulse - mean)
            + (.05 + .07 * smoothLoad) * knock
            + .018 * filteredNoise * knockWindow * smoothLoad;
        double alpha = 1 - Math.Exp(-Math.Tau * (380 + 220 * smoothLoad) * dt);
        dieselExhaust += alpha * (raw - dieselExhaust);
        dieselBody += alpha * (dieselExhaust - dieselBody);
        double saturated = .85 * Math.Tanh(dieselBody * (1.25 + .35 * smoothLoad));
        dieselTail += (1 - Math.Exp(-Math.Tau * 650 * dt)) * (saturated - dieselTail);
        return dieselTail;
    }

    double Plymouth(double dt)
    {
        var pcm = PlymouthSample.Pcm;
        int index = (int)plymouthPosition;
        int next = index + 1 == pcm.Length ? 0 : index + 1;
        double fraction = plymouthPosition - index;
        double sample = (pcm[index] + (pcm[next] - pcm[index]) * fraction) / short.MaxValue;
        // An idle recording is texture, not a variable-speed recording of a full rev range.
        double playback = Math.Clamp(smoothRpm / PlymouthSample.ReferenceRpm, .85, 1.25);
        plymouthPosition = (plymouthPosition + PlymouthSample.SampleRate * dt * playback) % pcm.Length;
        double texture = .06 * (1 - SmoothStep((smoothRpm - 700) / 900));
        return V8(dt, true) * .94 + sample * texture;
    }

    double V8(double dt, bool plymouth = false)
    {
        // Crossplane firing order 1-8-4-3-6-5-7-2: each bank has uneven
        // 90/180/270-degree gaps. Separate banks retain the characteristic burble
        // instead of collapsing eight firings into a high, uniform sine tone.
        double left = Pulse(crank) + Pulse(crank - 3d / 8) + Pulse(crank - 5d / 8) + Pulse(crank - 6d / 8);
        double right = Pulse(crank - 1d / 8) + Pulse(crank - 2d / 8) + Pulse(crank - 4d / 8) + Pulse(crank - 7d / 8);
        // Subtract the analytic pulse mean, then mix unequal exhaust paths.
        const double bankMean = 4 * (1d / 18 - 1d / 110);
        double banks = (left - bankMean) * .95 + (right - bankMean) * (plymouth ? .72 : .45);
        double cycle = crank * Math.Tau;
        double rumble = (plymouth ? .70 : .62) * Math.Sin(cycle * 4) + .12 * Math.Sin(cycle * 6 + .4);
        double firing = .24 * Math.Sin(cycle * 8) + (.04 + .04 * smoothLoad) * Math.Sin(cycle * 16);
        double raw = banks * .75 + rumble + firing + .018 * filteredNoise * smoothLoad;
        // Two exhaust low-pass poles: throttle adds growl, never a bright hiss.
        double cutoff = 650 + 700 * smoothLoad;
        double alpha = 1 - Math.Exp(-Math.Tau * cutoff * dt);
        exhaust += alpha * (raw - exhaust);
        exhaustBody += alpha * (exhaust - exhaustBody);
        // Normal mode retains natural dynamics; distortion belongs to the overdrive control.
        double body = exhaustBody * (1.15 + .35 * smoothLoad);
        exhaustTail += (1 - Math.Exp(-Math.Tau * 1800 * dt)) * (body - exhaustTail);
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
