using ScooterSound.Core;

// Keep these input sequences stable: the v3.1 hashes were captured before changing the renderer.
internal static class AudioScenarios
{
    public static short[] Render(Motor motor, int rate, string scenario, AudioSettings? settings = null)
    {
        var profile = EngineProfile.All[(int)motor];
        var synth = new Synthesizer();
        bool ride = scenario == "ride";
        if (ride) synth.Start();
        int blocks = ride ? 2200 : 400;
        var engine = new EngineSimulator();
        var pcm = new short[blocks * (rate / 100)];
        var requested = settings ?? AudioSettings.Default;
        for (int block = 0; block < blocks; block++)
        {
            double t = block / 100d;
            EngineSnapshot snapshot;
            double volume = requested.Volume;
            if (ride)
            {
                double speed = t < 3 ? 0 : t < 11 ? (t - 3) / 8 * 25 : t < 14 ? 25 : Math.Max(0, 25 - (t - 14) * 5);
                if (block % 10 == 0) engine.PushSpeed(speed / 3.6, 5, t, t);
                snapshot = engine.Tick(t, profile);
                if (motor == Motor.Turbine) volume *= t < 8 ? .8 : t < 16 ? .35 : 1;
            }
            else
            {
                double rpm = scenario == "idle" ? profile.IdleRpm : scenario == "cruise" ? 1600 : profile.MaxRpm;
                double load = scenario == "idle" ? .08 : scenario == "cruise" ? .4 : 1;
                snapshot = new(10, 0, rpm, load, 2, DrivingState.Cruising, false);
            }
            synth.Render(pcm.AsSpan(block * (rate / 100), rate / 100), snapshot, motor, requested with { Volume = volume }, rate);
        }
        return pcm;
    }
}
