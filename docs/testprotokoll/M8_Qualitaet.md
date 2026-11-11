# Testprotokoll M8: Qualität und Abschluss

Stand: 11.11.2026, Windows 11, .NET 10, Build `Release` (Performance) bzw. `Debug` (übrige Tests). Alle Tests laufen mit Fakes,
temporären Dateien oder Demo-Daten; kein echter Dienst, keine echte Aufgabe, kein echtes Ereignisprotokoll.

## 1. Performance (NFA01, Technische Schuld Nr. 7)

Start: `$env:MORPHIX_PERFORMANCE = 1; dotnet test -c Release --filter Kategorie=Performance --logger "console;verbosity=detailed"`
(Quelle: `tests/Morphix.Tests/Qualitaet/PerformanceTests.cs`, Generator mit festem Seed in `LogGenerator.cs`; ohne die Variable werden die
Tests übersprungen.)

| Messgröße | 100.000 Zeilen (7,0 MB) | 1.000.000 Zeilen (70,1 MB) | Vorgabe |
|---|---|---|---|
| Einlesen und Parsen | 0,11 s (99.840 Einträge) | 1,36 s (998.398 Einträge) | NFA01: unter 5 s für 100.000 Zeilen |
| Fehler gruppieren | < 0,01 s | 0,06 s | – |
| Filter Schweregrad + Text | 1 ms | 9 ms | NFA01: unter 1 s |
| Filter Suchbegriff | 3 ms | 31 ms | NFA01: unter 1 s |
| Gehaltener Speicher (verwaltet, nach GC) | 16 MB | 162 MB | – |
| Spitze Arbeitsspeicher (ganzer Testprozess, inkl. Generator) | 481 MB | 481 MB | – |

**Bewertung:** NFA01 ist mit großem Abstand erfüllt. Die Einträge im Speicher zu halten (Technische Schuld Nr. 7) genügt auch für eine
Million Zeilen. Die Spezifikation enthält keine Anforderung „NFA11“ (sie endet bei NFA10); der Umsetzungsplan nennt sie für den
Test mit einer Million Zeilen. Dafür gilt hier ein selbst gewählter Rahmen (unter 60 s, unter 2 GB gehaltener Speicher), der deutlich
unterschritten wird. Nicht gemessen: Darstellung in der Oberfläche (die Tabelle zeigt höchstens 10.000 Einträge, Nr. 8) und der Export.

## 2. Robustheit (NFA07)

`RobustheitUndIntegritaetTests.NFA07_Ordner_mit_zehn_fehlerhaften_Dateien`: ein Ordner mit zehn fehlerhaften Dateien und einer guten.

| Nr. | Datei | Erwartung | Ergebnis |
|---|---|---|---|
| 1 | leer | keine Einträge, kein Fehler | bestanden |
| 2 | Zufallsbytes (binär) | keine Einträge, kein Absturz | bestanden |
| 3 | gültige Zeilen mit NUL-Bytes | wird gelesen | bestanden |
| 4 | Windows-1252 mit Umlauten | Fallback, Umlaute bleiben erhalten | bestanden |
| 5 | UTF-16 mit BOM | wird gelesen | bestanden |
| 6 | ohne Zeitstempel | keine Einträge | bestanden |
| 7 | nur Leerzeilen | keine Einträge | bestanden |
| 8 | eine Zeile mit 2 MB | wird gelesen | bestanden |
| 9 | unbekannte Schweregrade | als Info gelesen | bestanden |
| 10 | gesperrt (`FileShare.None`) | übersprungen und gemeldet | bestanden |
| 11 | gute Datei (Kontrolle) | Eintrag wird gelesen | bestanden |

Die Diagnose ist abgeschlossen, genau eine Datei (die gesperrte) steht unter „übersprungen“, zehn Dateien gelten als gelesen, und der
Bericht lässt sich erzeugen. Ein zweiter Test prüft einen Ordner nur aus unbrauchbaren Dateien: leere, aber gültige Diagnose.

## 3. Integrität (NFA08, GR05)

`NFA08_Originale_bleiben_bei_Analyse_und_Export_unveraendert`: Drei Logdateien (UTF-8, Windows-1252, binär) mit festem Änderungsdatum.
Vor und nach Analyse, Bericht und ZIP-Export (mit und ohne Maskierung) werden SHA-256, Änderungsdatum (UTC), Größe und Dateiliste
verglichen. **Ergebnis: bestanden, alles unverändert.** Der Test `DateiLogLeserTests.Originaldateien_bleiben_unveraendert` prüft dasselbe für das
Einlesen allein; `HtmlZipExporterTests` und `DemoBerichtIntegrationTests` prüfen die Kopien im ZIP und die Demo-Originale.

## 4. Abdeckung (NFA10)

`dotnet test --collect:"XPlat Code Coverage"`, Zeilenabdeckung (Cobertura):

| Projekt | Zeilen | Zweige |
|---|---|---|
| **Morphix.Core** | **85,4 %** (Ziel: mindestens 70 %) | 79,4 % |
| Morphix.Infrastructure | 83,9 % | 75,3 % |
| Morphix.App | 81,1 % | 75,2 % |

Nicht abgedeckt sind bewusst die dünnen Schichten über dem echten System (`ServiceControllerZugriff`, `TaskSchedulerZugriff`,
`EventLogZugriff`; manuell bzw. gar nicht, siehe Technische Schuld Nr. 9 und 17), die Fensterklassen (`BestaetigungsDialog`,
`ExportDialog`, `WebBrowserHtml`) und die beiden Modelltypen `Aufgabenlauf` und `Dienstaktion` (nur Datenträger für spätere Funktionen).
Im Core fehlen damit nur diese zwei reinen Datensätze.

## 5. Gesamtlauf

`dotnet test`: 344 Tests (342 bestanden), davon 2 Performance-Tests ohne Freigabe übersprungen, 0 Fehler, 0 Warnungen. (Die Abdeckung in Abschnitt 4 wurde vor den letzten elf Testfällen für `AnzeigeTexte` gemessen; Core liegt seitdem geringfügig höher.)
`dotnet publish src/Morphix.App -c Release -r win-x64 --self-contained false -o release` erzeugt `release/`; die veröffentlichte
`Morphix.App.exe` startet mit der installierten .NET 10 Desktop Runtime, zeigt das Hauptfenster und reagiert.
