# Plymouth-V8-Referenz

`PlymouthIdle.pcm` ist ein aus der vom Nutzer bereitgestellten MP3 gewonnener
Auspuff-Loop: PCM16 Little Endian, mono, 11025 Hz, 3,850 Sekunden, 84892 Bytes.

Quelle: `telegram-cloud-document-2-5409099125595615900.mp3.mp3` (9,267 Sekunden).
SHA-256 der Quelldatei:
`43d63e2fc399b753521c1692f76d2148f43b2a1ca55e03a1d21f77d761afe6fb`.

Verwendet wird der Abschnitt 2,2–6,2 Sekunden, mit 150 ms Überblendung an der
Schleifengrenze. Tiefste Frequenzen und obere Rauschanteile werden gefiltert,
Pegel und Gleichspannungsanteil normalisiert. Die Aufnahme selbst wird beim
App-Build nicht benötigt. Das kleine PCM-Sample ist als Assembly-Ressource eingebettet.

Reproduktion mit NumPy, nach Dekodierung der MP3 in eine 16-Bit-WAV-Datei:

```sh
ffmpeg -i reference.mp3 -c:a pcm_s16le reference.wav
python3 tools/extract-plymouth-loop.py reference.wav ScooterSound.Core/Audio/PlymouthIdle.pcm
```

Bei der Auswertung lagen nach Sekunde 2 überwiegend Bassanteile vor, häufige
Spektralspitzen etwa bei 70–85 Hz und 130–155 Hz. Die Wiedergabe-Referenzdrehzahl
von 1100 U/min ist eine Abstimmung, keine verifizierte Messung der Motordrehzahl.
Auch Modell, Hubraum und Baujahr des aufgenommenen Motors sind nicht verifiziert.
Die Aufnahme wurde technisch ausgewertet; direktes Anhören war in der Sitzung
nicht verfügbar. Ein Hörvergleich auf geeigneten Lautsprechern steht aus.
