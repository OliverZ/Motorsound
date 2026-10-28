# Änderungen

## 2.0

- Tieferer Crossplane-V8 mit unregelmäßigen Zündimpulsen beider Zylinderbänke,
  mehr Bass, gedämpften Höhen und kräftigerem Auspuffklang unter Last.
- V8-Leerlauf bei 620 U/min, maximal 4400 U/min und niedrigere Reisedrehzahlen.
- Synthetischer Anlasser, Zündmoment und weicher Übergang in den Leerlauf beim
  Start einer Fahrt; die Startsequenz funktioniert mit allen fünf Motorprofilen.
- GPS-Verarbeitung auf einem eigenen Thread; Android 12+ fordert präzise,
  ungepufferte Standortupdates an. CPU-Wachhaltung vor der GPS-Anmeldung.
- Neuer Einstieg „GPS bei ausgeschaltetem Display“ für Akkuoptimierung,
  Energiesparmodus und herstellerspezifische App-Einstellungen.
- Erweiterte Audioprüfungen und reproduzierbare V8-Hörprobe als WAV.

Kernprüfungen bei 44,1/48 kHz und Android-ARM64-Build erfolgreich.
GPS bei gesperrtem Bildschirm und das Klangbild müssen zusätzlich auf einem
echten Android-Gerät geprüft werden; ein Emulator wurde nicht eingerichtet.

Die ARM64-APK ist ein eigenständig installierbarer Debug-Build für Android 8.0
oder neuer. Sie ist mit einem anderen Debugschlüssel als die APK aus Release
1.0 signiert. Eine vorhandene Version 1.0 muss deshalb vor der Installation
deinstalliert werden; dabei werden die lokalen App-Einstellungen gelöscht.
