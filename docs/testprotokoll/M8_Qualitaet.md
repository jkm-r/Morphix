# Testprotokoll M8: Qualität und Abschluss

Stand: 11.11.2026, Windows 11, .NET 10, Build `Release` (Performance) bzw. `Debug` (übrige Tests). Alle Tests laufen mit Fakes,
temporären Dateien oder Demo-Daten; kein echter Dienst, keine echte Aufgabe, kein echtes Ereignisprotokoll.

## 1. Performance und Grenzwerte (NFA01, NFA11 laut Anforderung v1.1)

Anforderung NFA11 (v1.1): bis 1.000.000 Logzeilen bzw. 500 Dateien ohne Absturz; Einlesezeit höchstens linear wachsend und unter 50 s;
Arbeitsspeicher unter 1 GB; Profile mit bis zu 50 Diensten und 50 Aufgaben; Statusabfrage aller Dienste unter 1 s.

Start der Lasttests: `$env:MORPHIX_PERFORMANCE = 1; dotnet test -c Release --filter Kategorie=Performance --logger "console;verbosity=detailed"`
(Quelle: `tests/Morphix.Tests/Qualitaet/PerformanceTests.cs`, Generator mit festem Seed in `LogGenerator.cs`; ohne die Variable werden die
Tests übersprungen). Die Grenzwerte für Profile und Dienste (`GrenzwertTests.cs`) laufen in jedem `dotnet test`.

### 1.1 Logzeilen (NFA01 und NFA11)

| Messgröße | 20.000 (Aufwärmen) | 100.000 Zeilen (7,0 MB) | 1.000.000 Zeilen (70,1 MB) | Vorgabe |
|---|---|---|---|---|
| Einlesen und Parsen | 0,08 s | 0,23 s (99.840 Einträge) | 1,50 s (998.398 Einträge) | NFA01: 100.000 in unter 5 s; NFA11: unter 50 s |
| Wachstum | – | – | Faktor 6,5 für das Zehnfache an Zeilen | NFA11: höchstens linear (Faktor 10; im Test Toleranz 20) |
| Fehler gruppieren | 0,01 s | 0,01 s | 0,06 s | – |
| Filter Schweregrad + Text | 1 ms | 1 ms | 19 ms | NFA01: unter 1 s |
| Filter Suchbegriff | 1 ms | 3 ms | 44 ms | NFA01: unter 1 s |
| Gehaltener Speicher (verwaltet, nach GC) | 3 MB | 36 MB | 317 MB | NFA11: Arbeitsspeicher unter 1 GB |
| Spitze Arbeitsspeicher (ganzer Testprozess, inkl. Generator) | 92 MB | 118 MB | 469 MB | NFA11: unter 1 GB |

In einem eigenständigen Lauf von `NFA01_100000_Zeilen` (ohne vorherige Messungen im selben Prozess) ergaben sich 0,12 s und 16 MB. Der gehaltene
Speicher für eine Million Zeilen schwankt zwischen Läufen (162 MB bei der ersten Messung am selben Tag, 317 MB hier), je nachdem, was der
Speicherbereiniger von den vorherigen Messungen noch nicht freigegeben hat; beide Werte liegen weit unter der Grenze.

### 1.2 500 Dateien

`NFA11_500_Dateien`: 500 Logdateien mit je 2.000 Zeilen (zusammen 1.000.000 Zeilen) in einem Ordner.

| Messgröße | Ergebnis | Vorgabe |
|---|---|---|
| Einlesen und Parsen | 1,58 s (999.000 Einträge, 500 Dateien gelesen, keine übersprungen) | unter 50 s |
| Gehaltener Speicher | 159 MB | unter 1 GB |
| Spitze Arbeitsspeicher | 495 MB | unter 1 GB |

### 1.3 Profile mit 50 Diensten und 50 Aufgaben, Statusabfrage

`GrenzwertTests` (nur Fakes, kein echter Dienst, keine echte Aufgabe):

| Prüfpunkt | Ergebnis |
|---|---|
| Profil mit 50 Diensten und 50 Aufgaben ist gültig (GR02, Feldregeln) | bestanden |
| Speichern als JSON und Laden liefert dasselbe Profil | bestanden |
| Statusabfrage aller 50 Dienste (`WindowsDienstGateway` über einen Fake der Dienstverwaltung), Reihenfolge bleibt erhalten | bestanden, im Test unter 1 s (Messwert im Millisekundenbereich) |
| Ganze Analyse mit 50 Diensten und 50 Aufgaben (Fakes) | bestanden, unter 1 s |

**Einschränkung:** Gemessen wird der Aufwand von Morphix selbst. Die Antwortzeit des echten Service Control Managers für 50 Dienste wurde nicht gemessen,
weil Tests keine echten Dienste ansprechen dürfen. Die Dienste werden in `WindowsDienstGateway` nacheinander abgefragt; pro Dienst ist das ein lokaler
Aufruf. Ein rein lesender Messlauf mit realen Diensten ist nur nach ausdrücklicher Freigabe vorgesehen.

**Bewertung:** NFA01 und NFA11 (v1.1) sind erfüllt, mit großem Abstand zu allen Grenzen. Die Einträge im Speicher zu halten (Technische Schuld
Nr. 7) genügt auch für eine Million Zeilen. Nicht gemessen: Darstellung in der Oberfläche (die Tabelle zeigt höchstens 10.000 Einträge, Nr. 8) und der Export.

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

`dotnet test`: 388 Tests (385 bestanden), davon 3 Performance-Tests ohne Freigabe übersprungen, 0 Fehler, 0 Warnungen. (Die Abdeckung in Abschnitt 4 wurde vor den letzten elf Testfällen für `AnzeigeTexte` gemessen; Core liegt seitdem geringfügig höher.)
`dotnet publish src/Morphix.App -c Release -r win-x64 --self-contained false -o release` erzeugt `release/`; die veröffentlichte
`Morphix.App.exe` startet mit der installierten .NET 10 Desktop Runtime, zeigt das Hauptfenster und reagiert.
