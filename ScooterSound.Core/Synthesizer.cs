namespace ScooterSound.Core;

// Continuous, allocation-free PCM synthesis. Phase survives parameter changes.
public sealed class Synthesizer
{
    double phase, whine, filteredNoise, smoothRpm = 800, smoothLoad, smoothVolume;
    uint random = 0x12345678;
    public void Render(Span<short> output, EngineSnapshot snapshot, Motor motor, double volume, int sampleRate = 44100)
    {
        for (int i = 0; i < output.Length; i++)
        {
            smoothRpm += .0005 * (snapshot.Rpm - smoothRpm);
            smoothLoad += .0005 * (snapshot.Load - smoothLoad);
            smoothVolume += .0005 * (Math.Clamp(volume, 0, 1) - smoothVolume);
            double pulses = motor switch { Motor.V8 => 4, Motor.Motorcycle => 2, Motor.Turbine => 1, _ => 1 };
            double frequency = smoothRpm / 60 * pulses;
            phase = (phase + frequency / sampleRate) % 1;
            whine = (whine + (700 + smoothRpm * .28) / sampleRate) % 1;
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            double noise = random / (double)uint.MaxValue * 2 - 1;
            filteredNoise += .12 * (noise - filteredNoise);
            double p = phase * Math.Tau;
            double sound = motor switch
            {
                Motor.Scooter => .5 * Math.Sin(p) + .25 * Math.Sin(3 * p) + .12 * Math.Sin(7 * p) + .12 * noise * smoothLoad,
                Motor.Motorcycle => .55 * Math.Sin(p) + .24 * Math.Sin(2 * p) + .14 * Math.Sin(4 * p) + .08 * filteredNoise,
                Motor.V8 => .6 * Math.Sin(p) + .25 * Math.Sin(2 * p) + .1 * Math.Sin(3 * p) + .25 * filteredNoise * smoothLoad,
                Motor.Turbine => .3 * Math.Sin(whine * Math.Tau) + (.35 + .4 * smoothLoad) * filteredNoise + .08 * Math.Sin(p),
                _ => .5 * Math.Sin(p) + .22 * Math.Sin(2 * p) + .3 * Math.Pow(Math.Max(0, Math.Sin(p)), 12) + .15 * noise * smoothLoad
            };
            double gain = (.18 + .24 * smoothLoad) * smoothVolume;
            output[i] = (short)(Math.Clamp(sound * gain, -.95, .95) * short.MaxValue);
        }
    }
}
