# Scooter Sound

Android-App in .NET MAUI mit fünf synthetischen Motorprofilen: 50-ccm-Roller,
Motorrad, V8, Flugzeugturbine und Diesel. GPS-Geschwindigkeit steuert virtuelle
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

## Aufbau

- `ScooterSound.Core`: plattformfreie Filterung, Zustandsschätzung, Motorprofile und PCM-Synthese.
- `ScooterSound`: MAUI-Oberfläche und atomar ausgetauschte Zustandsabbilder.
- `Platforms/Android/RideService.cs`: GPS, Foreground-Service, Wake-Lock, Audiofokus und AudioTrack.
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

## Umfang und Prüfung

Die Sounds werden mathematisch erzeugt, es sind keine echten Motoraufnahmen enthalten.
Die fünf Profile unterscheiden sich in Klang, Drehzahlverlauf und Gangsimulation.
Sample-Layer, Aufnahmen und besondere Start-/Schalt-/Abstellgeräusche sind mögliche Erweiterungen.
GPS reagiert verzögert; insbesondere bei schlechter Sicht zum Himmel und niedriger Geschwindigkeit
bleibt die Zustandsschätzung begrenzt. Positionsdifferenzen dienen nur als eingeschränkter Rückfall.

Die Kernprüfungen wurden mit dem lokal eingerichteten .NET-10-SDK erfolgreich ausgeführt.
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
