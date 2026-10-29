#!/usr/bin/env python3
"""Extract the reference's stable section from a decoded 16-bit WAV.

Usage: python3 tools/extract-plymouth-loop.py reference.wav output.pcm
Requires numpy. Decode the supplied MP3 to WAV first (e.g. with ffmpeg).
The recording itself is not needed to build or run the app.
"""
import sys
import wave
from pathlib import Path
import numpy as np

with wave.open(sys.argv[1], 'rb') as source:
    if source.getsampwidth() != 2:
        raise ValueError('Expected a 16-bit PCM WAV')
    rate = source.getframerate()
    samples = np.frombuffer(source.readframes(source.getnframes()), dtype='<i2')
    samples = samples.reshape(-1, source.getnchannels()).mean(axis=1) / 32768

# Avoid the opening transient and final fade; retain several seconds of natural
# exhaust variation rather than repeating one identical firing pulse.
start, end, output_rate = 2.2, 6.2, 11025
if len(samples) / rate < end:
    raise ValueError('Reference must contain the complete 2.2–6.2 s section')
# Zero-pad before frequency-domain filtering to avoid circular edge artifacts.
padded = np.pad(samples, (rate, rate))
frequencies = np.fft.rfftfreq(len(padded), 1 / rate)
high_pass = 1 - np.exp(-(frequencies / 25) ** 4)
low_pass = 1 / np.sqrt(1 + (frequencies / 900) ** 8)
filtered = np.fft.irfft(np.fft.rfft(padded) * high_pass * low_pass, len(padded))[rate:-rate]
positions = np.arange(round((end - start) * output_rate)) / output_rate + start
loop = np.interp(positions * rate, np.arange(len(filtered)), filtered)
fade = round(.15 * output_rate)
blend = .5 - .5 * np.cos(np.linspace(0, np.pi, fade))
head = loop[:fade].copy()
loop = loop[fade:].copy()
loop[-fade:] = loop[-fade:] * (1 - blend) + head * blend
loop -= loop.mean()
loop *= min(.30 / np.sqrt(np.mean(loop ** 2)), .90 / np.max(np.abs(loop)))
path = Path(sys.argv[2])
path.parent.mkdir(parents=True, exist_ok=True)
path.write_bytes(np.rint(loop * 32767).astype('<i2').tobytes())
print(f'{path}: {len(loop) / output_rate:.3f} s, {output_rate} Hz, {path.stat().st_size} bytes')
