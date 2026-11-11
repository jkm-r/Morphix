# Änderungsprotokoll zu den Phase-1-Dokumenten

Dieses Protokoll hält fest, wo die Umsetzung die Vorgaben der Spezifikations- und Architekturdokumente
präzisiert oder ergänzt. Es gilt zusätzlich zu Kap. 6 des Architekturdokuments (.NET 10, `zeilenmuster`).

## 1. Präzisierung von P3 „Fehler gruppieren“ (Spezifikation, Kap. 2)

| | |
|---|---|
| **Vorgabe** | P3 b: „Meldung normalisieren: Zahlen, Datumsangaben, GUIDs und Dateipfade durch Platzhalter ersetzen“ – bezogen auf die Meldung. |
| **Umsetzung** | Als Meldungsmuster dient nur die **erste Zeile** der Meldung, normalisiert. Der vollständige (ggf. mehrzeilige) Text bleibt unverändert im `Logeintrag` und damit in der Fehlergruppe erhalten. |
| **Begründung** | Mehrzeilige Meldungen enthalten meist einen Stacktrace (GR06). Dieser unterscheidet sich zwischen Auftreten desselben Fehlers (andere Aufrufwege, Zeilennummern, Pfade) und würde gleichartige Fehler in viele Gruppen zerteilen. Die erste Zeile beschreibt den Fehler. |
| **Auswirkung** | `FehlerGruppierer.Gruppiere` gruppiert nach (Quelle, normalisierte erste Zeile). `Normalisiere` selbst ist unverändert. Abgesichert durch Tests in `FehlerGruppiererTests`. |
| **Reihenfolge der Ersetzung** | Unverändert laut Umsetzungsplan: GUIDs, Datum/Uhrzeit, Windows-Pfade, IPv4-Adressen, übrige Zahlen (inkl. Hexzahlen wie `0x1`). |

## 2. Selbst formulierte Validierungsmeldungen (Spezifikation, Kap. 5 „Eingabevalidierung“)

Die Spezifikation legt Meldungen nur für einen Teil der Regeln fest. Die Meldungen der Spalte
„Wortlaut“ sind unverändert übernommen; die übrigen sind ergänzt, weil GR02 bzw. die Feldregeln sie verlangen.

| Feld | Regel | Meldung | Quelle |
|---|---|---|---|
| Profilname | Pflicht, 3–50 Zeichen, eindeutig | „Bitte einen eindeutigen Namen mit 3 bis 50 Zeichen eingeben.“ | Wortlaut Spezifikation |
| Logordner | Pflicht, existiert, lesbar | „Der Ordner existiert nicht oder ist nicht lesbar.“ | Wortlaut Spezifikation |
| Dateimuster | Pflicht, gültiges Muster | „Bitte ein Dateimuster wie *.log angeben.“ | Wortlaut Spezifikation |
| Zeitraum (GR01) | von < bis, nicht Zukunft, ≤ 30 Tage | „Der Zeitraum ist ungültig (max. 30 Tage, nicht in der Zukunft).“ | Wortlaut Spezifikation |
| Logquellen | GR02: mindestens eine | „Bitte mindestens eine Logquelle angeben.“ | **selbst formuliert** |
| Dienste | GR02: mindestens einer | „Bitte mindestens einen Dienst angeben.“ | **selbst formuliert** |
| Dienstname | Pflicht (leer) | „Bitte einen Dienstnamen angeben.“ | **selbst formuliert** – die Spezifikation nennt für den Dienstnamen nur die Warnung „Dieser Dienst ist auf dem System nicht vorhanden.“ (nicht gefundener Dienst, keine Eingabesperre) |
| Protokollname (Ereignisanzeige) | Pflicht (leer) | „Bitte ein Protokoll angeben.“ | **selbst formuliert** – Ereignisanzeige-Quellen haben keinen Ordner und kein Dateimuster |

Außerdem ergänzt: Bei Logquellen vom Typ Ereignisanzeige entfallen Ordner- und Dateimusterprüfung, da
beides dort nicht zutrifft.

## 3. Demo-Daten nur mit IT-betrieblichen Inhalten (Umsetzungsplan, M3)

| | |
|---|---|
| **Vorgabe** | Umsetzungsplan M3 beschreibt Demo-Dienste `ZE-…`, Aufgaben wie „Nächtlicher Lohnexport“ und „Stammdatenabgleich“ sowie Logs mit Terminal-Timeouts. |
| **Umsetzung** | Morphix behandelt nur technische Themen, die den Betrieb eines Systems sicherstellen, keine fachlichen Funktionen. Die Demo-Daten enthalten deshalb keine Fachbegriffe der Zeiterfassung/Zeitwirtschaft und keine `ZE`-Namen mehr. |
| **Profil** | „Demo – Musterwerk Anwendungsserver“ |
| **Dienste** | `Muster-Anwendungsserver` (kritisch), `Muster-Kommunikationsdienst` (kritisch, im Demo-Zustand gestoppt), `Muster-Datenbankdienst`, `Muster-Sicherungsdienst` |
| **Aufgaben** | „Nächtliche Datensicherung“ (fehlgeschlagen, Code 0x1), „Zertifikatsprüfung“, „Protokollbereinigung“ |
| **Logs** | `muster-anwendungsserver.log`, `muster-kommunikation.log`, `muster-datenbank.log` (rund 2.800 Zeilen). Inhalt: Anmeldungen, Anfragen, Konfiguration, Lizenz- und Zertifikatswarnungen, Speicher-/Plattenplatz, Datenbankverbindung, Abfragezeiten und Verbindungspool, Verbindungs-Timeouts zu Gegenstellen. Wiederkehrende Fehlergruppen: „Timeout nach # s bei Gegenstelle #“, „Datenbankverbindung verloren“, „Datensicherung beendet mit Code #“. |
| **Nicht geändert** | Das Beispiel „Timeout nach 30 s bei Terminal 12“ bleibt in den Tests des `FehlerGruppierer`, weil es wörtlich in der Spezifikation (P3) steht. |

## 4. Zeitraum auf Tagesbasis und GR01-Grenze (Spezifikation GR01, Umsetzungsplan M4)

| | |
|---|---|
| **Vorgabe** | GR01: „von < bis, bis nicht in der Zukunft, höchstens 30 Tage“. |
| **Umsetzung** | Die Kopfleiste wählt ganze Kalendertage. „von“ = 00:00:00 des ersten Tages, „bis“ = 23:59:59 des letzten Tages, höchstens aber „jetzt“ (`ZeitraumRegel.AusTagen`). Standard sind die letzten 7 Tage, also heute und die 6 Tage davor. |
| **Grenze** | Geprüft wird `bis - von < 30 Tage`. Bei dieser Tagesbasis entspricht das höchstens 30 Kalendertagen (30 Tage von 00:00 bis 23:59:59 sind gültig, 31 nicht). Eine Dauer von exakt 30 mal 24 Stunden gilt damit nicht mehr als gültig. |
| **Meldung** | Unverändert der Wortlaut der Spezifikation; der Fehler erscheint rot an den Datumsfeldern und in der Statusleiste, „Analyse starten“ bleibt gesperrt. |

## 5. Dateiauswahl nach Änderungsdatum (Spezifikation P2, Umsetzungsplan M4)

| | |
|---|---|
| **Vorgabe** | P2: Dateien, „die im Analysezeitraum geändert wurden“; Plan M4: „Änderungsdatum im Zeitraum“. |
| **Umsetzung** | Der `DateiLogLeser` liest Dateien, deren Änderungsdatum **nicht vor dem Beginn** des Zeitraums liegt. Die Einträge selbst werden danach exakt auf den Zeitraum gefiltert. |
| **Begründung** | Eine Logdatei, in die nach dem Ende des Zeitraums weitergeschrieben wurde, hat ein Änderungsdatum nach „bis“, enthält aber trotzdem Einträge aus dem Zeitraum. Bei einer Auswahl „Änderungsdatum zwischen von und bis“ würde die Analyse einer vergangenen Woche aktive Logs auslassen. |

## 6. Platzhalter-Adapter für noch fehlende Windows-Adapter (Umsetzungsplan M4)

Die `WindowsDatenquellenFabrik` liefert für echte Profile den `DateiLogLeser`. Dienste, Aufgaben und
Ereignisse liefern bis zu den Meilensteinen M5 und M7 leere Ergebnisse (`LeeresDienstGateway`,
`LeeresAufgabenGateway`, `LeererEreignisLeser`) statt einer Ausnahme. Nur die Dienststeuerung (Aktion
ausführen) wirft weiterhin `NotSupportedException`, weil es dafür keine sinnvolle leere Antwort gibt; die
Oberfläche ruft sie vor M5 nicht auf.

## 7. Dienststeuerung im Demo-Modus und Bedienung (Spezifikation GR03/GR04, Umsetzungsplan M5)

| | |
|---|---|
| **GR03 im Demo-Modus** | GR03 verlangt Administratorrechte. Im Demo-Modus werden nur simulierte Dienste gesteuert (GR04); die Rechteprüfung kommt wie alle Datenquellen aus der Fabrik. Der Demo-Adapter `DemoRechtePruefer` meldet „Rechte vorhanden“, damit sich die Steuerung auch ohne erhöhte Rechte vorführen lässt („Im Demo-Modus lassen sich Dienste steuern“). Für echte Profile gilt unverändert der `WindowsRechtePruefer`. Der `DienstService` enthält dafür kein `if (istDemo)`. Profilzugehörigkeit und Bestätigung werden auch im Demo-Modus geprüft. |
| **Simuliertes System** | Die Fabrik liefert pro Demo-Profil dasselbe `DemoDienstGateway`, solange sich die Dienstliste nicht ändert. Dadurch stimmen Dienste-Ansicht und Dashboard überein: Ein im Demo gestarteter Dienst bleibt für die Dauer der Sitzung gestartet. |
| **Schaltflächen** | Zusätzlich zu den Adminrechten sind die Schaltflächen vom Status abhängig: „Starten“ nur bei gestoppten/angehaltenen, „Stoppen“ bei laufenden/angehaltenen, „Neustarten“ nur bei laufenden Diensten. Nicht gefundene Dienste lassen sich nicht steuern. |
| **Bestätigungsdialog** | „Dienst X wirklich stoppen?“ mit den Schaltflächen **Ja** und **Abbrechen** (eigener Dialog, da das Standard-Meldungsfenster kein „Ja/Abbrechen“ anbietet). „Abbrechen“ ist die Standardschaltfläche, damit die Eingabetaste nichts steuert. |
| **Hinweis ohne Rechte** | Statusleiste: „Ohne Administratorrechte: Dienststeuerung gesperrt“; in der Ansicht zusätzlich ein Hinweistext. |

## 8. Bericht, Maskierung der Logkopien und Export (Spezifikation GR09/GR10/US14/US15, Umsetzungsplan M6)

| | |
|---|---|
| **Maskierung der Logs im ZIP** | Der Umsetzungsplan sieht „Kopien der gelesenen Dateien“ im ZIP vor, das Architekturdokument (Kap. 4) verlangt, dass ungeschützte Daten den Rechner nicht verlassen. Beides zusammen geht nur, wenn die Maskierung auch die Logkopien erfasst. Bei aktiver Maskierung enthält das ZIP deshalb **zeilenweise maskierte Kopien** (UTF-8); bei abgeschalteter Maskierung byte-genaue Kopien. Die Originale werden nur gelesen (GR05). |
| **Schnittstelle `IBerichtExporter`** | Dafür bekommt `ExportierenAsync` den optionalen Parameter `zeilenFilter` (Maskierung wird in `BerichtService` festgelegt, das Kopieren bleibt in der Infrastruktur). Neu ist `KannSchreiben(zielpfad)` für die Prüfung „Exportordner beschreibbar“ („In diesen Ordner kann nicht geschrieben werden.“, Wortlaut Spezifikation). Die Prüfung legt eine Probedatei mit `DeleteOnClose` an; im Ordner bleibt nichts zurück. |
| **Maskierung ausschalten** | GR10 verlangt „bewusst pro Bericht“. Umsetzung: Das Abschalten der Option fragt über den Bestätigungsdialog nach; ohne „Ja“ bleibt sie an. Jede neue Analyse und jeder Profilwechsel schaltet die Maskierung wieder ein. Ist sie aus, warnt die Ansicht, und der Bericht nennt es im Abschnitt „Systeminformationen“. |
| **Rechnername** | Bei aktiver Maskierung steht im Bericht `[Rechner]` statt des Rechnernamens (Umsetzungsplan: „Rechnername maskierbar“). |
| **Inhalt** | Systeminformationen, Zusammenfassung, Dienststatus, Fehlerübersicht (je Gruppe die jüngste Meldung im Wortlaut), Aufgaben und Ereignisse nur, wenn vorhanden (US13), übersprungene Dateien. Ereignisse sind Logeinträge mit der Quelle `Logeintrag.EreignisQuelle` („Ereignisanzeige“); der Leser aus M7 muss diese Quelle setzen. |
| **Begrenzungen** | Höchstens 100 Fehlergruppen, 100 Ereignisse und 4.000 Zeichen je Meldung im HTML. Der Bericht sagt, wenn etwas fehlt; vollständig sind die Logs im ZIP. |
| **Nicht kopierbare Logs** | Eine gesperrte oder gelöschte Logdatei bricht den Export nicht ab (NFA07). Sie wird in `logs/_nicht_kopiert.txt` im ZIP vermerkt. Gleich heißende Dateien aus verschiedenen Ordnern erhalten ein Suffix (`app_2.log`). |
| **Vorschau** | `WebBrowser`-Steuerelement von WPF (kein zusätzliches Paket nötig). Es nutzt die ältere Internet-Explorer-Engine; das CSS im Bericht ist deshalb bewusst einfach (keine Variablen, kein Grid). Das Anzeigen läuft über eine Attached Property (`WebBrowserHtml.Html`) und blockiert Navigation zu Webadressen (NFA06). |
| **HTML-Kodierung** | `WebUtility.HtmlEncode` kodiert Zeichen von 160 bis 255 numerisch (`ü` wird `&#252;`). Der Browser zeigt sie korrekt an; im Quelltext sind Umlaute aus Logs daher als Entitäten zu sehen. Feste Überschriften sind nicht betroffen. |
