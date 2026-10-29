# Scooter Sound

Aktuelle Version: **2.0**. Änderungen und Installationshinweise: [CHANGELOG.md](CHANGELOG.md).

Android-App in .NET MAUI mit sechs Motorprofilen: 50-ccm-Roller,
Motorrad, V8, Flugzeugturbine, Diesel und Plymouth V8. GPS-Geschwindigkeit steuert virtuelle
Drehzahl, Last, Schaltverhalten und Klang. Kein Backend, keine Standortübertragung.

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

Beim Start ertönt für etwa 1,65 Sekunden ein synthetischer Anlasser mit Zündmoment,
kurzem Hochlaufen und Übergang in den Leerlauf. Bei bereits rollendem Fahrzeug geht
die Startsequenz anschließend weich in den GPS-gesteuerten Fahrklang über.
Der V8 ist auf einen tiefen Crossplane-Klang abgestimmt: unregelmäßige Impulse zweier
Zylinderbänke, tiefe Kurbelwellenanteile, ein gefilterter Auspuffklang und mehr Druck
unter Last. Sein Leerlauf liegt bei 620 U/min, die Obergrenze bei 4400 U/min;
gleichmäßige Fahrt nutzt niedrigere Drehzahlen als Beschleunigen.

Zusätzlich steht **Plymouth V8** zur Auswahl: ein eigener, auf der bereitgestellten
Aufnahme basierender Auspuffklang, 680 U/min Leerlauf, maximal 4000 U/min und drei
virtuelle Gänge. Ein 83-KiB-Loop mit weichem Übergang erhält die natürlichen
Schwankungen der Aufnahme. GPS steuert die Tonhöhe; Last erhöht Lautstärke und
Sättigung. Die Startsequenz bleibt synthetisch. Quelle und Verarbeitung sind unter
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

Fünf Profile werden mathematisch erzeugt; Plymouth V8 verwendet einen bearbeiteten
Ausschnitt der Nutzeraufnahme. Die sechs Profile unterscheiden sich in Klang,
Drehzahlverlauf und Gangsimulation. Besondere Schalt-/Abstellgeräusche sind mögliche Erweiterungen.
GPS reagiert verzögert; insbesondere bei schlechter Sicht zum Himmel und niedriger Geschwindigkeit
bleibt die Zustandsschätzung begrenzt. Positionsdifferenzen dienen nur als eingeschränkter Rückfall.

Die Kernprüfungen wurden mit dem lokal eingerichteten .NET-10-SDK erfolgreich ausgeführt.
Sie prüfen zusätzlich die Startsequenzen aller Profile bei 44,1/48 kHz, blockunabhängige
PCM-Kontinuität, Stummstellung, V8-Lastdynamik, Bass/Höhen-Verhältnis, Aussteuerungsreserve,
Gleichspannungsanteil, niedrige Reisedrehzahl sowie GPS-Ausfall und Wiederaufnahme.
Eine signierte, eigenständig installierbare Debug-APK lässt sich mit `./build-apk.ps1` erzeugen;
sie landet unter `artifacts/` und richtet sich standardmäßig an ARM64-Geräte.
Der ARM64-Debug-Build wurde erfolgreich erstellt und die APK-Signatur mit `apksigner`
geprüft (v2 und v3). Mindestversion Android 8, Zielversion Android 16 / API 36.
Ein Laufzeittest auf einem Android-Gerät steht noch aus.
Vor Verwendung folgende Geräteprüfungen durchführen:

- Alle Profile im Testmodus von 0 auf 25 km/h und zurück, Motorwechsel und Stummstellung.
- GPS-Fahrt mit Anfahren, konstantem Tempo, Verzögern und Stillstand.
- Standort verweigert, nur ungefährer Standort, GPS ausgeschaltet und Empfangsausfall.
- Bildschirmsperre, App-Wechsel und Stopp aus der Benachrichtigung.
- Anruf/andere Audio-App sowie Bluetooth-/Kopfhörertrennung.
- Wiederholtes Starten/Stoppen und mindestens 20 Minuten Betrieb ohne Audioaussetzer.

Android-Diensttypen: https://developer.android.com/develop/background-work/services/fgs/service-types
Standort bei ausgeschaltetem Bildschirm: https://developer.android.com/develop/sensors-and-location/location/permissions
