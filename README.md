# Scooter Sound

Aktuelle Version: **3.2** (Versionscode 7). Änderungen und Installationshinweise: [CHANGELOG.md](CHANGELOG.md).

Android-App in .NET MAUI mit sechs Motorprofilen: 50-ccm-Roller,
Motorrad, V8, Flugzeugturbine, Diesel und Plymouth V8. GPS-Geschwindigkeit steuert virtuelle
Drehzahl, Last, Schaltverhalten und Klang. Kein Backend, keine Standortübertragung.

Die Motorauswahl zeigt **Plymouth V8 → V8-Auto → Diesel → Motorrad →
50-ccm-Roller → Flugzeugturbine**. Die Kolbenmotoren sind nach Größenkategorie
geordnet; die Turbine steht separat am Ende. Bestehende Motorauswahlen bleiben
auch nach der Umsortierung erhalten.

## Starten

Voraussetzung: .NET 10 SDK, MAUI-Android-Workload und die dazu passenden Android-SDK-/JDK-Versionen
(am einfachsten über eine aktuelle Visual-Studio-Installation mit MAUI-Workload).

```powershell
dotnet workload install maui-android
dotnet restore .\ScooterSound\ScooterSound.csproj
dotnet build .\ScooterSound\ScooterSound.csproj -f net10.0-android
# Verbundenes Android-Gerät oder laufender Emulator:
dotnet build .\ScooterSound\ScooterSound.csproj -t:Run -f net10.0-android
```

Android 8.0 oder neuer. Projekt direkt über `ScooterSound/ScooterSound.csproj` öffnen.
Für reale Fahrten präzisen Standort erlauben und GPS einschalten. Die Benachrichtigung
zeigt die aktive Fahrt und bietet eine Stopp-Aktion. Der Testmodus funktioniert ohne
Standortberechtigung: vor dem Start aktivieren und die Geschwindigkeit am Regler ändern.
Motorwechsel und Lautstärke sind während der Fahrt möglich. Motor und Lautstärke werden gespeichert.

## Lautstärke und Gaspedal

Die fünf Kolbenmotoren erhalten mehr durchschnittlichen Pegel durch Profilabgleich,
RMS-Kompression und eine eigene Ausgangsstufe. **Die Turbine verwendet unverändert
die Klangerzeugung und Pegelregelung aus 3.1**, einschließlich Startsequenz und Masterlautstärke.

- **Boost:** 0 bis +18 dB, Voreinstellung +6 dB. Die Anhebung erfolgt nach der Kompression;
  sie wird nicht durch eine nachfolgende automatische Normalisierung aufgehoben.
- **Overdrive:** 0–100 %, Voreinstellung 0 %. Optionaler weicher Overdrive mit vierfacher
  Überabtastung und Anti-Aliasing-Filter; bei 0 % wird der Effekt umgangen.
- **Audio zurücksetzen:** App-Lautstärke 100 %, Boost +6 dB, Overdrive 0 %.
- Boost und Overdrive sind bei der Turbine deaktiviert und mit „Nur für Kolbenmotoren“ markiert.
  Die Einstellungen bleiben für den nächsten Kolbenmotor erhalten.

Neue Einstellungen werden gespeichert und während der Wiedergabe über 50 ms geglättet.
Gegenüber 3.1 ergeben die identischen Tests bei 100 % App-Lautstärke für den V8
**+6,54 dB RMS im Leerlauf und +4,98 dB bei 1600 U/min / 40 % Last**. Beim Plymouth
sind es **+5,47 bzw. +4,38 dB**. Andere Kolbenmotoren gewinnen in diesen Zuständen
etwa 1,8–3,1 dB; die Turbine bleibt samplegenau gleich. Das sind digitale Messwerte,
keine Messungen des Schalldrucks am Bluetooth-Lautsprecher.

Die Spitzenbegrenzung der Kolbenmotoren lässt höchstens −0,5 dBFS zu; bei sehr hohem Boost
begrenzt sie entsprechend stärker. +18 dB Boost bedeutet daher keine garantierte Anhebung
um 18 dB am Lautsprecher. Die App-Lautstärke wirkt zuletzt und kann auch bei maximalem
Boost/Overdrive vollständig stummschalten. Bestehende Lautstärkeeinstellungen bleiben erhalten;
**MAX** setzt den App-Regler auf 100 %. Android-Medienlautstärke und Lautsprecherlautstärke
werden separat am jeweiligen Gerät eingestellt.

Nach dem Start **GAS · GEDRÜCKT HALTEN** betätigen oder den Gaspedal-Regler
dosieren. Beim Loslassen geht das Pedal auf null und die Drehzahl fällt weich
zurück. Das funktioniert auch im Stand. Ohne GPS den Testmodus aktivieren und
mit unverändertem Geschwindigkeitsregler bei 0 km/h starten. Das Pedal verändert
nur Drehzahl und Last, niemals die GPS-Geschwindigkeit. Beim Stoppen, Motorwechsel,
Seitenwechsel oder Wechsel in eine andere App wird das Pedal zurückgesetzt.
Die Bedienung auf einem echten Android-Gerät steht noch zur Prüfung aus.

```sh
dotnet run --project ScooterSound.Core.Checks -- --preview-pedal artifacts/v8-gaspedal-laut.wav
```

Diese Hörprobe zeigt Start, Leerlauf und drei Gasstöße bei weiterhin 0 km/h.

## Klang

Der Diesel verwendet einen tiefen Reihensechser-Klang mit 650 U/min Leerlauf,
maximal 3200 U/min und fünf virtuellen Gängen. Drei Verbrennungsimpulse je
Kurbelwellenumdrehung, ein gedämpfter Auspuffklang und kurze mechanische
Impulsanteile ersetzen die frühere Mischung mit durchgehendem Rauschen.
Eine Hörprobe mit Start, Leerlauf und Gasstößen im Stand erzeugt:

```sh
dotnet run --project ScooterSound.Core.Checks -- --preview-diesel artifacts/diesel-ueberarbeitet.wav
```

Beim Start ertönt für etwa 1,65 Sekunden ein synthetischer Anlasser mit Zündmoment,
kurzem Hochlaufen und Übergang in den Leerlauf. Bei bereits rollendem Fahrzeug geht
die Startsequenz anschließend weich in den GPS-gesteuerten Fahrklang über.
Der V8 ist auf einen tiefen Crossplane-Klang abgestimmt: unregelmäßige Impulse zweier
Zylinderbänke, tiefe Kurbelwellenanteile, ein gefilterter Auspuffklang und mehr Druck
unter Last. Sein Leerlauf liegt bei 620 U/min, die Obergrenze bei 4400 U/min;
gleichmäßige Fahrt nutzt niedrigere Drehzahlen als Beschleunigen.

**Plymouth V8** verwendet jetzt einen gleichmäßig laufenden Crossplane-V8 als Klangbasis:
680 U/min Leerlauf, maximal 4000 U/min und drei virtuelle Gänge. Der vorhandene
83-KiB-Loop liefert nur noch dezente Leerlauftextur (maximal 6 % vor dem Profilabgleich),
mit auf 0,85–1,25 begrenzter Wiedergaberate. Zwischen 700 und 1600 U/min blendet die
Textur weich aus; höhere Drehzahlen stammen aus der Motorsynthese. Der Loop wird
nicht länger bis zur Höchstdrehzahl hochgepitcht. Die frühere zusätzliche Plymouth-Sättigung
entfällt. Beide V8 erhalten hörbarere Auspuff-Obertöne für Bluetooth-Lautsprecher.
Die Startsequenz bleibt synthetisch. Quelle und Verarbeitung sind unter
[Audio/README.md](ScooterSound.Core/Audio/README.md) dokumentiert.

Eine reproduzierbare, 22 Sekunden lange V8-Hörprobe lässt sich ohne Android erzeugen:

```sh
dotnet run --project ScooterSound.Core.Checks -- --preview artifacts/v8-start-fahrt.wav
dotnet run --project ScooterSound.Core.Checks -- --preview-plymouth artifacts/plymouth-v8-start-fahrt.wav
```

Die Hörprobe enthält Start/Leerlauf (0–3 s), Beschleunigen (3–11 s), konstante Fahrt
(11–14 s), Verzögern (14–19 s) und Rückkehr zum Leerlauf (19–22 s). Für die Beurteilung
des Bassanteils Kopfhörer oder einen bassfähigen Lautsprecher verwenden.

## Aufbau

- `ScooterSound.Core`: plattformfreie Filterung, Zustandsschätzung, Motorprofile und PCM-Synthese.
- `ScooterSound`: MAUI-Oberfläche und atomar ausgetauschte Zustandsabbilder.
- `Platforms/Android/RideService.cs`: GPS auf eigenem Thread, Foreground-Service, Wake-Lock, Audiofokus und AudioTrack.
- `ScooterSound.Core.Checks`: ausführbare Kernprüfungen ohne zusätzliche Testpakete.

```powershell
dotnet run --project .\ScooterSound.Core.Checks
```

Die Audioausgabe rendert kontinuierliche 20-ms-PCM-Blöcke; GPS aktualisiert getrennt davon.
Geschwindigkeit wird geglättet, unplausible Sprünge werden verworfen. Nach drei Sekunden
ohne brauchbare Messung sinken Geschwindigkeit und Sound kontrolliert auf Leerlauf.
Beschleunigung und Verzögerung werden geschätzt; eine echte Gas-/Bremsstellung ist nicht bekannt.
Der virtuelle Fahrbereich ist zunächst auf 25 km/h abgestimmt.

Der Dienst wird ausschließlich vom Nutzer aus der sichtbaren App gestartet und bleibt
bei ausgeschaltetem Bildschirm aktiv. Es wird keine Hintergrundstandortberechtigung
angefordert. Audiofokusverlust (z. B. Anruf) oder getrennte Kopfhörer/Bluetooth-Ausgabe
stoppen die Fahrt; zum Fortsetzen erneut starten. Nach Prozessbeendigung startet der
Dienst nicht automatisch neu. Gerätespezifische Energiesparmechanismen können den Dienst beeinflussen.

Für GPS bei gesperrtem Bildschirm in der App **GPS bei ausgeschaltetem Display**
öffnen. Dort kann Scooter Sound ausdrücklich von der Akkuoptimierung ausgenommen
werden. Je nach Hersteller außerdem unter Apps → Scooter Sound → Akku
**Uneingeschränkt** bzw. Hintergrundaktivität erlauben. Der systemweite Energiesparmodus
kann GPS bei Display-Aus trotz laufendem Dienst abschalten oder drosseln; diesen für
die Fahrt ausschalten. Die App zeigt die Systemeinstellungen nur auf Tastendruck an.
Die Ausnahme erhöht während einer Fahrt gegebenenfalls den Akkuverbrauch.

Der Dienst hält die CPU bereits vor der GPS-Anmeldung wach. Standortcallbacks laufen
auf einem eigenen Thread, unabhängig vom UI-Timer; Android 12+ erhält eine explizite
High-Accuracy-Anforderung ohne Batching. Ein echter Empfangsausfall führt weiterhin
zum Leerlauf; frische Messungen setzen die GPS-Steuerung automatisch fort.
Der bisherige Aufbau enthielt bereits einen Foreground-Service und Wake-Lock;
die konkrete Ursache des gemeldeten Display-Aus-Problems ist ohne Gerätetest nicht
bestätigt. Ein zusätzlicher Hintergrundstandortzugriff wird dafür nicht angefordert.

## Umfang und Prüfung

Alle Profile verwenden Motorsynthese; Plymouth V8 ergänzt eine dezente Textur aus der
Nutzeraufnahme. Die sechs Profile unterscheiden sich in Klang,
Drehzahlverlauf und Gangsimulation. Besondere Schalt-/Abstellgeräusche sind mögliche Erweiterungen.
GPS reagiert verzögert; insbesondere bei schlechter Sicht zum Himmel und niedriger Geschwindigkeit
bleibt die Zustandsschätzung begrenzt. Positionsdifferenzen dienen nur als eingeschränkter Rückfall.

Die Kernprüfungen wurden mit dem lokal eingerichteten .NET-10-SDK erfolgreich ausgeführt.
Sie prüfen zusätzlich die Startsequenzen aller Profile bei 44,1/48 kHz, blockunabhängige
PCM-Kontinuität, Stummstellung, V8-Lastdynamik, Bass/Höhen-Verhältnis, Aussteuerungsreserve,
Gleichspannungsanteil, niedrige Reisedrehzahl sowie GPS-Ausfall und Wiederaufnahme.
`./build-apk.ps1` erzeugt eine ARM64-Release-APK ohne Debugsymbole und AOT-Vorkompilierung
unter `artifacts/ScooterSound-3.2-android-arm64.apk`. `-Configuration Debug` ist optional.
Mindestversion Android 8, Zielversion Android 16 / API 36. Die Buildoptionen sind:

```sh
dotnet build ScooterSound/ScooterSound.csproj -t:SignAndroidPackage -c Release -f net10.0-android -p:RuntimeIdentifier=android-arm64 -p:AndroidPackageFormats=apk -p:EmbedAssembliesIntoApk=true -p:RunAOTCompilation=false -p:DebugSymbols=false -p:DebugType=None
```

**Signierung:** Der bisherige Schlüssel ist unter `signing/release.keystore` verfügbar
und durch `.gitignore` ausgeschlossen. Die APK 3.2 wurde damit neu signiert; ihr
Zertifikats-Fingerprint (SHA-256) stimmt mit dem GitHub-Release 3.1 überein:
`c18242f05f7fe5a0906a772fd2b169ebb50fe3a45971fc27bcbf10a3a71b5251`.
Damit ist ein direktes Update über 3.1 ohne Deinstallation möglich. Die Signatur
wurde mit `apksigner` geprüft (v2/v3). Erneuter Build:

```powershell
# Passwörter nur in der Umgebung setzen, nicht ins Repository schreiben.
# ANDROID_SIGNING_STORE_PASSWORD und ANDROID_SIGNING_KEY_PASSWORD müssen gesetzt sein.
.\build-apk.ps1 -KeyStore '.\signing\release.keystore' -KeyAlias androiddebugkey
```

Reproduzierbare Audiomessungen und WAV-Hörproben aller Profile:

```powershell
dotnet run --project tools/AudioAudit -- artifacts/audio-v3.2.json artifacts/after
```

Die Baseline aus 3.1 liegt unter `ScooterSound.Core.Checks/Fixtures/audio-v3.1.json`.
Die Regression prüft samplegenaue Turbinen-Hashes für Start/Fahrt und stationäre Zustände
bei 44,1/48 kHz auch mit maximalen Boost-/Overdrive-Einstellungen. Beide V8 müssen im
Leerlauf und bei 1600 U/min / 40 % Last mindestens 4 dB mehr RMS-Pegel liefern als 3.1.
Zusätzlich werden Extremwerte, Regleränderungen, Überabtastung über Blockgrenzen,
Stummstellung und Motorwechsel geprüft.
Die Audit-Ausgabe enthält außerdem V8-/Plymouth-Hörproben mit 100 % Overdrive zum
direkten Vergleich mit dem normalen Klang bei 0 %.

Ein Laufzeittest auf einem Android-Gerät und ein Hörvergleich auf dem Bluetooth-Lautsprecher
stehen noch aus; digitale Pegelmessungen ersetzen diese Klangabnahme nicht.
Vor Verwendung folgende Geräteprüfungen durchführen:

- Alle Profile im Testmodus von 0 auf 25 km/h und zurück, Motorwechsel und Stummstellung.
- GPS-Fahrt mit Anfahren, konstantem Tempo, Verzögern und Stillstand.
- Standort verweigert, nur ungefährer Standort, GPS ausgeschaltet und Empfangsausfall.
- Bildschirmsperre, App-Wechsel und Stopp aus der Benachrichtigung.
- Anruf/andere Audio-App sowie Bluetooth-/Kopfhörertrennung.
- Wiederholtes Starten/Stoppen und mindestens 20 Minuten Betrieb ohne Audioaussetzer.

Android-Diensttypen: https://developer.android.com/develop/background-work/services/fgs/service-types
Standort bei ausgeschaltetem Bildschirm: https://developer.android.com/develop/sensors-and-location/location/permissions
