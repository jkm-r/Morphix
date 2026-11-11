# Morphix

Morphix ist eine Windows-Desktopanwendung für Administratoren und Support-Mitarbeitende von
Zeiterfassungs- und Zutrittskontrollsystemen. Sie sammelt Logdateien, Windows-Ereignisse,
Dienststatus und Ergebnisse der Aufgabenplanung an einem Ort, gruppiert Fehler und erzeugt einen
standardisierten Support-Bericht (HTML, als ZIP mit den Logs exportierbar). Morphix behandelt
ausschließlich IT-technische Betriebsthemen und übernimmt keine fachlichen Funktionen.

Studienportfolio (IU, „Projekt: Software Engineering“). Die verbindlichen Vorgaben stehen in `docs/`.

## Voraussetzungen

- Windows 10 oder 11 (64 Bit)
- Zum Ausführen: [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64)
- Zum Bauen: .NET 10 SDK

Morphix greift nicht auf das Netzwerk zu und sendet keine Daten. Logdateien werden nur lesend geöffnet.

## Bauen, testen, starten

```
dotnet build
dotnet test
dotnet run --project src/Morphix.App
```

Abdeckung messen: `dotnet test --collect:"XPlat Code Coverage"`.

Performance-Tests (große Logdateien, laufen nur auf Anforderung):

```
$env:MORPHIX_PERFORMANCE = 1
dotnet test -c Release --filter Kategorie=Performance --logger "console;verbosity=detailed"
```

### Ausliefern

```
dotnet publish src/Morphix.App -c Release -r win-x64 --self-contained false -o release
```

Der Ordner `release/` enthält danach `Morphix.App.exe` samt Demo-Daten (`demo/`). Er ist nicht im
Repository, sondern wird bei Bedarf erzeugt. Zum Starten genügt die installierte .NET 10 Desktop Runtime.

## Benutzeranleitung

### Schnellstart im Demo-Modus (ohne eigenes System)

1. Morphix starten. Beim ersten Start legt Morphix das Profil **„Demo – Musterwerk Anwendungsserver“** an
   und wählt es aus. In der Kopfleiste erscheint der Hinweis „Demo-Modus“.
2. Der Zeitraum steht auf den letzten 7 Tagen. **„Analyse starten“** klicken; ein Fortschrittsbalken läuft.
3. Im **Dashboard** erscheinen die Kennzahlen (Fehler, gestoppte kritische Dienste, fehlgeschlagene
   Aufgaben) und die häufigsten Fehlergruppen.
4. In der Ansicht **Bericht** den Bericht prüfen und **„Als ZIP exportieren“** wählen.

Im Demo-Modus sind alle Daten simuliert, auch die Dienste: Es wird kein echter Dienst gesteuert.

### Profil anlegen oder ändern

Ein Profil beschreibt ein System. **„Neu“** legt ein Profil an, **„Bearbeiten“** ändert das gewählte.

- **Name:** eindeutig, 3 bis 50 Zeichen.
- **Logquellen** (mindestens eine): Typ *Datei* mit Ordner und Dateimuster (z. B. `*.log`) oder Typ
  *Ereignisanzeige* mit dem Protokollnamen (z. B. `Application`).
- **Dienste** (mindestens einer): technischer Dienstname (z. B. `Spooler`), optional Anzeigename. Ein als
  *kritisch* markierter Dienst wird hervorgehoben, wenn er nicht läuft.
- **Geplante Aufgaben** (optional): Name der Aufgabe in der Aufgabenplanung, auch als voller Pfad
  (`\Microsoft\Windows\Defrag\ScheduledDefrag`).

Fehler werden direkt am Feld rot markiert; „Speichern“ bleibt bis zur Korrektur gesperrt. Profile liegen als
JSON-Dateien unter `%AppData%\Morphix\Profile`.

### Analyse

Zeitraum über **von/bis** wählen (ganze Tage, höchstens 30 Tage, nicht in der Zukunft), dann
**„Analyse starten“**. Die Statusleiste zeigt, wie viele Dateien (und wie viele Megabyte) eingelesen wurden und
ob Dateien übersprungen werden mussten (z. B. gesperrte). Unlesbare Dateien stoppen die Analyse nicht.

### Die Ansichten

| Ansicht | Inhalt |
|---|---|
| **Dashboard** | Kennzahlen und die fünf häufigsten Fehlergruppen. „Bericht erstellen“ führt zur Berichtsansicht. |
| **Logs und Fehler** | Fehlergruppen mit Anzahl sowie erstem und letztem Auftreten. Filter nach Schweregrad, Suchbegriff (höchstens 200 Zeichen) und Zeitraum wirken sofort. Doppelklick auf eine Gruppe zeigt ihre Einträge. Ereignisse der Ereignisanzeige erscheinen mit der Quelle „Ereignisanzeige“. |
| **Dienste** | Status und Starttyp der Profildienste, alle 5 Sekunden aktualisiert. Ein gestoppter kritischer Dienst ist rot. **Starten**, **Stoppen**, **Neustarten** erfordern Administratorrechte und eine Bestätigung und gelten nur für Dienste des Profils. Ohne Administratorrechte ist die Ansicht nur lesend. |
| **Aufgaben** | Letzter Lauf, Ergebnis und nächster Lauf der Profilaufgaben. Eine Aufgabe mit letztem Ergebniscode ungleich 0 ist rot (fehlgeschlagen). |
| **Bericht** | Vorschau des Support-Berichts, Option **„Sensible Daten maskieren“**, **„Als ZIP exportieren“** und **„Im Browser öffnen“**. |

### Bericht erstellen und exportieren

1. Analyse durchführen. Der Bericht entsteht nur aus einer abgeschlossenen Analyse.
2. In der Ansicht **Bericht** die Vorschau prüfen. Der Bericht enthält Systeminformationen, Dienststatus,
   Fehlerübersicht, Aufgaben und Ereignisse (sofern vorhanden) sowie übersprungene Dateien.
3. **„Als ZIP exportieren“**: Ort und Dateiname wählen (Vorschlag `Morphix_<Profil>_<Datum>_<Uhrzeit>.zip`). Das ZIP
   enthält `bericht.html` und den Ordner `logs/` mit Kopien der gelesenen Logdateien. Die Originale bleiben
   unverändert. Exportierte ZIP-Dateien gehören nicht ins Repository (`.gitignore`).
4. **„Im Browser öffnen“** zeigt den Bericht als temporäre Datei im Standardbrowser.

**Maskierung:** Standardmäßig sind IP-Adressen, Benutzernamen und der Rechnername unkenntlich gemacht, im
Bericht **und** in den Logkopien im ZIP. Sie lässt sich pro Bericht nach einer Rückfrage abschalten; bei der
nächsten Analyse ist sie wieder an.

## Aufbau

| Projekt | Inhalt |
|---|---|
| `src/Morphix.Core` | Domäne, Schnittstellen und Anwendungsservices (ohne Windows-Bezug) |
| `src/Morphix.Infrastructure` | Windows-Adapter und Demo-Adapter |
| `src/Morphix.App` | WPF-Oberfläche (Views, ViewModels, DI-Start) |
| `tests/Morphix.Tests` | Unit-, Integrations-, Robustheits- und Performance-Tests |
| `demo/` | Demo-Profil und Demo-Logdateien |
| `docs/` | Projektdokumente, Änderungsprotokoll, technische Schulden, Testprotokolle |

Stand: Meilenstein M8 (Qualität und Abschluss). Umsetzungsstand und bewusste Abweichungen:
`docs/UMSETZUNGSPLAN.md`, `docs/AENDERUNGEN.md`, `docs/TECHNICAL_DEBT.md`.

## Demo-Daten neu erzeugen

Die Demo-Logdateien unter `demo/logs` entstehen reproduzierbar (fester Seed) aus dem Skript
`tools/Erzeuge-DemoDaten.ps1`:

```
pwsh tools/Erzeuge-DemoDaten.ps1
```
