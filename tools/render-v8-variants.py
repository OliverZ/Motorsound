#!/usr/bin/env python3
"""Render V8 audition concepts without changing the app's selected profiles.

Requires numpy; optional libmp3lame provides MP3 copies. PCM source is the small
Plymouth loop already embedded in ScooterSound.Core. Outputs are reproducible.
"""
import ctypes as ct
import ctypes.util
import json
from pathlib import Path
import wave
import zipfile
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'artifacts/v8-auswahl'
RATE, SECONDS = 44100, 20
TAU = 2 * np.pi
VARIANTS = [
    dict(file='01_Big_Block_Thunder', name='Big Block Thunder', description='Sehr tief, schwer und druckvoll; dunkler Big-Block-Charakter.', idle=580, maximum=3200, sample=.38, synthetic=.90, lope=.045, bank=.40, drive=1.65, cutoff=530, bass=1.35, presence=.08, pops=.07),
    dict(file='02_Cam_Lope_Muscle', name='Cam Lope Muscle', description='Unruhiger, stolpernder Leerlauf; rauer Muscle-Car-Charakter.', idle=660, maximum=3600, sample=.65, synthetic=.65, lope=.16, bank=.23, drive=2.05, cutoff=720, bass=1.0, presence=.28, pops=.12),
    dict(file='03_Open_Headers', name='Open Headers', description='Bissig und metallischer; harte Auspuffimpulse mit mehr Mitten.', idle=790, maximum=4300, sample=.30, synthetic=1.0, lope=.055, bank=.65, drive=2.35, cutoff=1250, bass=.70, presence=.70, pops=.16),
    dict(file='04_Dark_Dragster', name='Dark Dragster', description='Dicht, verzerrt und aggressiv; starke Gasstöße und Abtouren.', idle=710, maximum=4000, sample=.45, synthetic=.92, lope=.12, bank=.31, drive=3.1, cutoff=880, bass=1.20, presence=.48, pops=.24),
    dict(file='05_Plymouth_Brutal', name='Plymouth Brutal', description='Am stärksten von deiner Aufnahme geprägt; tief und grobkörnig.', idle=600, maximum=3200, sample=1.0, synthetic=.25, lope=.07, bank=.48, drive=2.5, cutoff=610, bass=1.25, presence=.12, pops=.09),
]


def smooth(x):
    x = np.clip(x, 0, 1)
    return x*x*(3-2*x)


def filter_sound(x, cutoff, bass=0, presence=0):
    # Analogue-style two-pole exhaust, DC/infra filtering, broad body resonances.
    pad = 4096
    padded = np.pad(x, (pad, pad))
    f = np.fft.rfftfreq(len(padded), 1 / RATE)
    hp = (1j*f / (20 + 1j*f)) ** 2
    lp = 1 / (1 + 1j*f/cutoff) ** 2
    safe = np.maximum(f, .01)
    body = np.exp(-.5*(np.log(safe/85)/.65)**2)
    bite = np.exp(-.5*(np.log(safe/360)/.60)**2)
    eq = 1 + bass*body + presence*bite
    return np.fft.irfft(np.fft.rfft(padded)*hp*lp*eq, len(padded))[pad:-pad]


def render(v):
    t = np.arange(RATE*SECONDS) / RATE
    # Same driver gestures for every candidate: starter, idle, two short blips,
    # one longer pull, then coast back to idle. Shapes have no step changes.
    rev = np.zeros_like(t)
    load = np.full_like(t, .09)
    for start, end, amount in [(4.5, 6.3, .48), (7.4, 10.1, .85), (12, 16, 1.0)]:
        opening = smooth((t-start)/.28)
        closing = 1-smooth((t-(end-.62))/.62)
        gesture = opening*closing*amount
        rev = np.maximum(rev, gesture)
        load = np.maximum(load, .09+.91*gesture)
    rpm = v['idle'] + (v['maximum']-v['idle'])*rev
    catch = smooth((t-.44)/.31)
    flare = v['idle']*.9*np.exp(-np.maximum(t-.70, 0)/.22)
    starting_rpm = 180 + catch*(v['idle']-180+flare)
    rpm = np.where(t<1.6, starting_rpm, rpm)
    # Cam lope: continuous speed and firing-strength changes, mainly at idle.
    uneven = np.sin(TAU*2.65*t)+.38*np.sin(TAU*4.21*t+.7)
    rpm *= 1 + v['lope']*(1-load)*uneven
    crank = np.cumsum(rpm / (120*RATE))
    angle = crank*TAU
    left, right = np.zeros_like(t), np.zeros_like(t)
    for index in range(8):
        phase = (crank-index/8) % 1
        pulse = np.exp(-phase*22)-np.exp(-phase*155)
        pulse *= 1+.06*np.sin(TAU*(1.1+index*.13)*t+index)
        if index in (0,3,5,6): left += pulse
        else: right += pulse
    bank_mean = 4*(1/22-1/155)
    banks = (left-bank_mean) + v['bank']*(right-bank_mean)
    throb = .32*np.sin(angle*4)+.19*np.sin(angle*6+.4)
    firing = .28*np.sin(angle*8)+.10*np.sin(angle*16+.3)
    synthetic = 1.8*banks+throb+firing
    texture = np.frombuffer((ROOT/'ScooterSound.Core/Audio/PlymouthIdle.pcm').read_bytes(), dtype='<i2').astype(float)/32767
    position = np.cumsum(rpm / 1100 * 11025 / RATE) % len(texture)
    idx = position.astype(int)
    recorded = texture[idx] + (texture[(idx+1)%len(texture)]-texture[idx])*(position-idx)
    rng = np.random.default_rng(5200)
    combustion_air = filter_sound(rng.standard_normal(len(t)), 1100)
    raw = v['synthetic']*synthetic + v['sample']*recorded*2
    raw += .055*combustion_air*(.2+load)*(np.maximum(left+right, 0)+.1)
    raw *= 1 + v['lope']*1.4*(1-load)*np.sin(TAU*2.65*t+.2)
    exhaust = filter_sound(raw, v['cutoff']*.85, v['bass']*.35)
    distorted = np.tanh(exhaust*(v['drive']+load*.85))
    dark = filter_sound(distorted, v['cutoff'], v['bass'], v['presence'])
    opened = filter_sound(distorted, v['cutoff']*1.5, v['bass']*.65, v['presence'])
    sound = dark*(1-load*.6)+opened*load*.6
    # Dry exhaust crackle after throttle release; short damped, filtered events.
    pops = np.zeros_like(t)
    for j, at in enumerate([6.1,6.27,9.93,10.08,10.3,15.85,16.06,16.26]):
        age = t-at
        gate = np.maximum(age, 0)
        burst = np.exp(-gate/(.022+j%3*.008))*(np.sin(TAU*(83+j*11)*gate)+.35*combustion_air)
        pops += np.where(age>=0, burst, 0)*v['pops']
    sound = sound*catch*(.46+.64*load) + filter_sound(pops, 1400)
    starter_envelope = smooth(t/.06)*(1-smooth((t-.46)/.28))
    compression = .55+.45*np.cos(TAU*11*t)
    starter = starter_envelope*compression*(.15*np.sin(TAU*235*t)+.05*np.sin(TAU*470*t)+.16*combustion_air)
    sound += starter
    # Compact exhaust/ground reflections, mono like the app, not a large hall.
    dry = sound.copy()
    for delay, level in [(.017,.10),(.031,.055),(.053,.025)]:
        n = round(delay*RATE)
        sound[n:] += dry[:-n]*level
    sound *= smooth(t/.02)*(1-smooth((t-(SECONDS-.08))/.08))
    # Match active-section RMS between concepts; smooth limiting retains headroom.
    active = (t>=2)&(t<17)
    gain = 1.0
    for _ in range(8):
        mastered = .88*np.tanh(sound*gain/.88)
        rms = np.sqrt(np.mean(mastered[active]**2))
        gain *= .23/max(rms, 1e-9)
    mastered = .88*np.tanh(sound*gain/.88)
    return mastered


def wav(path, samples):
    pcm = np.rint(np.clip(samples,-1,1)*32767).astype('<i2')
    with wave.open(str(path),'wb') as w:
        w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(pcm.tobytes())


def mp3(path, samples):
    # Optional local encoder. Never invokes a shell or changes the user's MP3.
    lib = ct.CDLL(ctypes.util.find_library('mp3lame'))
    lib.lame_init.restype = ct.c_void_p
    handle = lib.lame_init()
    for name, value in [('lame_set_in_samplerate',RATE),('lame_set_num_channels',1),('lame_set_brate',192),('lame_set_quality',2)]:
        fn=getattr(lib,name);fn.argtypes=[ct.c_void_p,ct.c_int];fn.restype=ct.c_int
        if fn(handle,value)<0: raise RuntimeError(name)
    lib.lame_init_params.argtypes=[ct.c_void_p];lib.lame_init_params.restype=ct.c_int
    if lib.lame_init_params(handle)<0: raise RuntimeError('MP3 init failed')
    pcm=np.rint(samples*32767).astype(np.int16)
    buffer=ct.create_string_buffer(int(len(pcm)*1.25)+7200)
    lib.lame_encode_buffer.argtypes=[ct.c_void_p,ct.POINTER(ct.c_short),ct.POINTER(ct.c_short),ct.c_int,ct.c_void_p,ct.c_int]
    lib.lame_encode_buffer.restype=ct.c_int
    ptr=pcm.ctypes.data_as(ct.POINTER(ct.c_short))
    size=lib.lame_encode_buffer(handle,ptr,ptr,len(pcm),buffer,len(buffer))
    if size<0: raise RuntimeError('MP3 encoding failed')
    data=buffer.raw[:size]
    lib.lame_encode_flush.argtypes=[ct.c_void_p,ct.c_void_p,ct.c_int];lib.lame_encode_flush.restype=ct.c_int
    size=lib.lame_encode_flush(handle,buffer,len(buffer))
    if size<0: raise RuntimeError('MP3 flush failed')
    data+=buffer.raw[:size]
    lib.lame_close.argtypes=[ct.c_void_p];lib.lame_close(handle)
    path.write_bytes(data)


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    comparison=[]
    measurements=[]
    for v in VARIANTS:
        samples=render(v)
        if not np.isfinite(samples).all(): raise ValueError('Non-finite PCM')
        peak=float(np.max(np.abs(samples)))
        rms=float(np.sqrt(np.mean(samples[2*RATE:17*RATE]**2)))
        dc=float(abs(np.mean(samples)))
        if peak>=.90 or abs(rms-.23)>.003 or dc>.005: raise ValueError(f'Invalid master: {v["file"]}')
        wav(OUT/(v['file']+'.wav'),samples)
        mp3(OUT/(v['file']+'.mp3'),samples)
        # Each candidate: 2 s idle, 2 s hard throttle, with soft edges and gaps.
        pieces=[]
        for start,end in [(2,4),(13,15)]:
            excerpt=samples[start*RATE:end*RATE].copy()
            fade=round(.025*RATE)
            excerpt[:fade]*=np.linspace(0,1,fade)
            excerpt[-fade:]*=np.linspace(1,0,fade)
            pieces.extend([excerpt,np.zeros(round(.15*RATE))])
        comparison.extend(pieces+[np.zeros(round(.35*RATE))])
        measurements.append(dict(name=v['name'],peak=round(peak,4),active_rms=round(rms,4),dc=round(dc,6)))
        print(v['file'], 'peak',round(peak,3),'active RMS',round(rms,3),flush=True)
    combined=np.concatenate(comparison)
    wav(OUT/'00_Direktvergleich.wav',combined)
    mp3(OUT/'00_Direktvergleich.mp3',combined)
    (OUT/'settings.json').write_text(json.dumps(dict(sample_rate=RATE,seconds=SECONDS,variants=VARIANTS,measurements=measurements),indent=2,ensure_ascii=False)+'\n')
    (OUT/'README.md').write_text('# V8-Klangauswahl\n\nFünf Klangentwürfe, keine authentifizierten Fahrzeugaufnahmen.\n'
        'Alle verwenden denselben Ablauf und denselben mittleren Pegel.\n'
        'Die Konzepte kombinieren synthetische Crossplane-Impulse mit dem bereits\n'
        'aus deiner Aufnahme gewonnenen Plymouth-Auspuff-Loop.\n\n'
        +'\n'.join(f'{i}. **{v["name"]}** – {v["description"]}' for i,v in enumerate(VARIANTS,1))
        +'\n\nJede Einzelprobe: 0–1,6 s Start, anschließend Leerlauf; Gasstöße bei\n'
        '4,5–6,3 s und 7,4–10,1 s, längeres Hochdrehen 12–16 s, dann Abtouren.\n'
        'Der Direktvergleich spielt je Kandidat zuerst Leerlauf und dann Gas,\n'
        'immer in der Reihenfolge 1 bis 5; Beginn etwa bei 0 / 4,65 / 9,30 / 13,95 / 18,60 s.\n\n'
        'WAV: 44,1 kHz, 16 Bit, mono. MP3: 192 kbit/s.\n'
        'Pegel angeglichen (RMS 0,23 im Abschnitt 2–17 s), Peak unter 0,90.\n'
        'Technisch auf Pegel/DC geprüft; direktes Anhören war hier nicht verfügbar.\n'
        'Die App-Profile und die veröffentlichte APK wurden nicht verändert.\n')
    with zipfile.ZipFile(OUT/'V8-Soundauswahl-MP3.zip','w',zipfile.ZIP_DEFLATED) as z:
        for path in sorted(OUT.glob('*.mp3')): z.write(path,path.name)
        for name in ['README.md','settings.json']: z.write(OUT/name,name)

if __name__=='__main__': main()
