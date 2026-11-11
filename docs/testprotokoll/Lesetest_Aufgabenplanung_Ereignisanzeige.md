# Testprotokoll: Lesetest auf dem echten System (Aufgabenplanung und Ereignisanzeige)

| | |
|---|---|
| **Datum** | 11.11.2026 |
| **Zweck** | Die beiden dünnen Schichten über dem echten System prüfen, die in automatischen Tests nie laufen: `TaskSchedulerZugriff` und `EventLogZugriff` (siehe `TECHNICAL_DEBT.md`, Punkt 17). |
| **Freigabe** | Ausdrücklich nur **lesender** Zugriff auf die Aufgabe `\Microsoft\Windows\Defrag\ScheduledDefrag` und das Protokoll „Application“. |
| **Durchführung** | Kleines Wegwerf-Konsolenprogramm außerhalb des Repos, das nur `TaskSchedulerZugriff.Lesen`, `EventLogZugriff.Lesen`, `WindowsAufgabenGateway.AbfragenAsync` und `WindowsEreignisLeser.LesenAsync` aufruft. Danach gelöscht. |
| **Nicht getan** | Es wurde keine Aufgabe gestartet, beendet, geändert oder gelöscht, kein Dienst angefasst, kein Ereignisprotokoll geleert, exportiert oder beschrieben. |

## 1. Aufgabenplanung

| Prüfpunkt | Eingabe | Ergebnis | Bewertung |
|---|---|---|---|
| Einfacher Name, Suche in Unterordnern (`FindTask`) | `ScheduledDefrag` | gefunden (rund 160 ms) | bestanden |
| Letzter Lauf | – | 09.11.2026 09:47 | bestanden |
| `LastTaskResult` | – | `0x0` (erfolgreich) → `LetztesErgebnis = 0`, nicht fehlgeschlagen (GR08) | bestanden |
| Nächster Lauf | – | keiner geplant (`NextRunTime` leer → `null`, Anzeige „–“) | bestanden |
| Über `WindowsAufgabenGateway` | `ScheduledDefrag` | Lauf, Ergebnis 0, kein nächster Lauf übernommen | bestanden |
| Unbekannte Aufgabe | `MorphixGibtEsNicht_Test` | nicht gefunden; Gateway gibt die Aufgabe unverändert zurück | bestanden |
| **Voller Pfad** | `\Microsoft\Windows\Defrag\ScheduledDefrag` | **zuerst nicht gefunden** | **Mangel** |

**Mangel und Behebung:** `FindTask` sucht nach Namen, nicht nach Pfaden. Trägt jemand im Profil den vollen Pfad ein, meldete Morphix die Aufgabe als „nicht gefunden“. `TaskSchedulerZugriff` löst Namen mit Backslash jetzt über `GetTask` auf. Wiederholter Lesetest:

| Eingabe | Ergebnis |
|---|---|
| `\Microsoft\Windows\Defrag\ScheduledDefrag` | gefunden (gleiche Werte wie oben) |
| `Microsoft\Windows\Defrag\ScheduledDefrag` (ohne führenden `\`) | gefunden |
| `ScheduledDefrag` | gefunden |
| `\Microsoft\Windows\Defrag\GibtEsNicht` | nicht gefunden |
| `MorphixGibtEsNicht_Test` | nicht gefunden |

Die in `TECHNICAL_DEBT.md` offene Annahme „`FindTask` findet Aufgaben in Unterordnern“ ist damit bestätigt. Die Statuscodes „noch nicht gelaufen“, „läuft“ und „wartet“ ließen sich hier nicht beobachten, weil die Aufgabe weder lief noch wartete; sie sind nur über Fakes getestet.

## 2. Ereignisanzeige, Protokoll „Application“

Zeitraum: die letzten 7 Tage (05.11. bis 11.11.2026), höchstens 5.000 Ereignisse.

| Prüfpunkt | Ergebnis | Bewertung |
|---|---|---|
| Lesen über `EventLogZugriff` | 289 Ereignisse in rund 530 ms | bestanden |
| Stufen | nur Fehler (Stufe 2): 205, Warnungen (Stufe 3): 84; keine Information, kein ausführliches Ereignis | bestanden (Filter wirkt) |
| Zeitraum | alle Ereignisse liegen im angefragten Zeitraum | bestanden |
| Meldungstext | 267 von 289 mit Beschreibung; 22 ohne (Meldungsdatei des Anbieters fehlt) | bestanden: Diese erscheinen mit „(keine Beschreibung verfügbar)“ |
| Häufigste Anbieter | Application Error (50), .NET Runtime (45), VSS (32) | zur Plausibilität |
| Über `WindowsEreignisLeser` | 289 `Logeintrag`-Objekte (205 Fehler, 84 Warnung), aufsteigend nach Zeit sortiert, Quelle immer „Ereignisanzeige“ | bestanden |

Der Protokolltext der Ereignisse ist hier bewusst nicht wiedergegeben (kann Benutzer- oder Pfadangaben enthalten).

## 3. Folgerungen

- `TaskSchedulerZugriff` und `EventLogZugriff` funktionieren auf dem Testsystem wie vorgesehen; ein Mangel (voller Pfad) wurde gefunden und behoben.
- Die Prüfung der Pfadauflösung ist bisher nur dieser manuelle Test; automatisiert lässt sie sich nicht abdecken, ohne das echte System anzufassen.
- Offen bleibt ein Test des Protokolls „System“ und eines Protokolls ohne Leserecht (z. B. „Security“ ohne Adminrechte). Beides wird im `DiagnoseService` über Fakes als „übersprungen“ gemeldet.
