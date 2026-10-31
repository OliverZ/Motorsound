# Änderungen

## 3.2

- Lautere Kolbenmotoren mit Profilabgleich, RMS-Kompression, Boost und Spitzenbegrenzung
  bei −0,5 dBFS. Masterlautstärke bleibt nach der Verarbeitung wirksam.
- Separate, gespeicherte Regler für Boost (0 bis +18 dB, Standard +6 dB) und Overdrive
  (0–100 %, Standard 0 %). Overdrive mit vierfacher Überabtastung; Regler werden geglättet.
- Turbine verwendet vollständig den bisherigen Verarbeitungspfad aus 3.1. Neue Regler
  sind für dieses Profil deaktiviert. Golden-Hashes sichern die samplegenaue Ausgabe ab.
- Beide V8 erhalten mehr hörbare Auspuff-Obertöne. Plymouth nutzt eine gleichmäßige
  Crossplane-Synthese; der Aufnahme-Loop bleibt dezente Leerlauftextur mit begrenzter
  Wiedergaberate und Ausblendung bis 1600 U/min. Keine zusätzliche Plymouth-Sättigung.
- Reproduzierbare Vorher-/Nachher-Messungen, Turbinen-Referenzen und Tests bei 44,1/48 kHz.
- Versionscode 7. ARM64-Release-Build; Buildskript unterstützt einen expliziten Keystore
  und Passwörter aus Umgebungsvariablen.

Die APK 3.2 wurde mit dem inzwischen kopierten Originalschlüssel aus
`signing/release.keystore` neu signiert. Der Zertifikats-Fingerprint stimmt mit
Release 3.1 überein; direkte Updates sind ohne Deinstallation möglich.
Das Buildskript entfernt vor dem Signieren ausschließlich die erzeugten signierten
APKs des aktuellen Buildordners, damit ein Schlüsselwechsel nicht durch den
inkrementellen Android-Build übergangen wird. Bluetooth-Hörvergleich und Bedienung
auf einem echten Android-Gerät sind weiterhin manuell zu prüfen.

## 3.1

- Motorauswahl nach Größe gruppiert: Plymouth V8, V8-Auto, Diesel, Motorrad,
  50-ccm-Roller; die Turbine folgt als Sonderprofil. Gespeicherte Motor-IDs
  werden unabhängig von der Reihenfolge wiederhergestellt.
- Diesel als tiefer Reihensechser neu abgestimmt: 650–3200 U/min, drei
  Zündungen je Kurbelwellenumdrehung, gefilterter Auspuff und kurze mechanische
  Verbrennungsimpulse mit geringem Rauschanteil.
- Hohes elektrisches Surren beim Start von V8-Auto, Plymouth V8 und Diesel entfernt:
  Der Anlasser verwendet keinen reinen 750-Hz-Ton mehr, sondern tief gefiltertes,
  mit den Kompressionstakten pulsierendes mechanisches Rattern.
- Zusätzliche Spektralprüfungen für Anlasser und Diesel bei 44,1/48 kHz sowie
  Prüfungen für die neue Anzeigereihenfolge und bestehende Motorauswahlen.

## 3.0

- Höherer Ausgangspegel aller sechs Profile durch gemeinsame Pegelanhebung,
  DC-Filterung und schnelle Spitzenbegrenzung; maximal etwa 95 % bei Vollgas.
- App-Lautstärke bei neuen Installationen standardmäßig 100 %, zusätzlicher
  MAX-Knopf; gespeicherte Benutzerwerte werden weiterhin berücksichtigt.
- Gaspedal als Haltetaste und dosierbarer Regler, auch im Stand und ohne GPS
  im Testmodus. Loslassen, App-Wechsel und Stoppen setzen das Pedal zurück.
- Drehzahl fällt nach dem Gaswegnehmen weicher ab. Das Pedal verändert keine
  GPS-Messwerte und wird getrennt von der simulierten Geschwindigkeit ausgewertet.
- Prüfungen für Standgasstöße, Rückkehr zum Leerlauf, GPS-Übernahme sowie
  Pegel und Lautstärkeregler aller Profile bei 44,1 und 48 kHz.

- Zusätzliches Plymouth-V8-Profil auf Basis der bereitgestellten MP3, mit tiefem
  Auspuff-Loop, lastabhängiger Sättigung, GPS-gesteuerter Tonhöhe und drei Gängen.
- Sample mit nur 83 KiB statt der vollständigen Aufnahme; Schleifenübergang weich
  überblendet, Gleichspannungsanteil und Höhen gefiltert.
- Auswahl und gespeicherte Motorprofile unterstützen jetzt sechs Einträge.
- Plymouth-Hörprobe sowie Prüfungen auf Schleifenknackser, Bassanteil und Übersteuerung.

Optimierte ARM64-APK als Release-Build ohne Debugsymbole und ohne AOT-Vorkompilierung.
Kernprüfungen bei 44,1/48 kHz, Android-Build und APK-Signatur geprüft.
Bedienung des Gaspedals, App-Wechsel und GPS bei gesperrtem Bildschirm müssen
zusätzlich auf einem echten Android-Gerät getestet werden.

Die APK verwendet denselben Signaturschlüssel wie Release 2.0 und kann darüber
installiert werden. Beim Wechsel direkt von 1.0 muss die alte App wegen des
anderen Signaturschlüssels vorher deinstalliert werden; dabei gehen lokale
App-Einstellungen verloren.

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
