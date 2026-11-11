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
| **Logs** | `muster-anwendungsserver.log`, `muster-kommunikation.log`, `muster-datenbank.log` (rund 2.800 Zeilen). Inhalt: Anmeldungen, Anfragen, Konfiguration, Lizenz- und Zertifikatswarnungen, Speicher-/Plattenplatz, Datenbankverbindung, Deadlock-freie Abfragezeiten, Verbindungs-Timeouts zu Gegenstellen. Wiederkehrende Fehlergruppen: „Timeout nach # s bei Gegenstelle #“, „Datenbankverbindung verloren“, „Datensicherung beendet mit Code #“. |
| **Nicht geändert** | Das Beispiel „Timeout nach 30 s bei Terminal 12“ bleibt in den Tests des `FehlerGruppierer`, weil es wörtlich in der Spezifikation (P3) steht. |
