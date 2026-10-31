# Audio baseline 3.1

`audio-v3.1.json` was captured from the unchanged renderer at commit
`9b3293c` before implementing 3.2 (Windows x64, .NET SDK 10.0.403/runtime 10.0.14).
Do not regenerate this fixture with the new renderer.

`AudioScenarios` retains the exact input trajectories: steady idle, 1600 rpm/40% load,
full load at maximum rpm, and a 22-second start/accelerate/cruise/decelerate/idle ride.
Steady metrics use the last two seconds of a four-second render. Hashes cover every
little-endian PCM16 sample including warmup. The turbine ride also changes the original
master volume at 8 and 16 seconds. Rates: 44100 and 48000 Hz.

The audit tool generates reports and WAV files using the same scenarios. The tests
compare turbine hashes to this immutable fixture and use the piston RMS measurements
as the before/after loudness reference. Auditory approval on the actual Bluetooth
speaker remains a separate device check.
