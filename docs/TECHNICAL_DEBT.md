# Technische Schulden und offene Punkte

Hier steht, was bewusst später oder anders umgesetzt wird, mit Begründung.

| Nr. | Thema | Stand | Begründung / Plan |
|---|---|---|---|
| 1 | Platzhalter-Adapter für Dienste, Aufgaben und Ereignisse bei echten Profilen | offen bis M5/M7 | `LeeresDienstGateway`, `LeeresAufgabenGateway` und `LeererEreignisLeser` liefern leere Ergebnisse (siehe `AENDERUNGEN.md`, Abschnitt 6). Mit M5 (`WindowsDienstGateway`) und M7 (`TaskSchedulerGateway`, `WindowsEreignisLeser`) werden sie in der Fabrik ersetzt und gelöscht. Bis dahin zeigt die Analyse echter Profile nur Logdaten. |
| 2 | ~~Tageslogik des Zeitraums~~ | erledigt in M4 | Siehe `AENDERUNGEN.md`, Abschnitt 4. |
| 3 | Defekte Profildateien werden still übersprungen | akzeptiert | `JsonProfilRepository` ignoriert unlesbare JSON-Dateien (NFA07), damit ein kaputtes Profil nicht alle anderen blockiert. Es gibt noch keine Anzeige, welche Datei übersprungen wurde. Ggf. in M8 über die Statusleiste melden. |
| 4 | Profile löschen ist nur im Repository, nicht in der Oberfläche | akzeptiert | Der Umsetzungsplan verlangt Anlegen, Speichern, Laden (US01). Löschen steht nicht in den Anforderungen. |
| 5 | Ordnerprüfung zur Eingabezeit | akzeptiert | `ProfilValidator` prüft die Existenz des Logordners bei jeder Änderung am Feld (Dateizugriff im UI-Thread, bei Netzlaufwerken ggf. träge). Für lokale Ordner unkritisch; bei Bedarf entkoppeln (Debounce/async). |
| 6 | Fortschritt und Abbruch der Analyse | akzeptiert | Der Fortschritt wird gemeldet (Statusleiste), die Analyse lässt sich aber nicht abbrechen. `ErstellenAsync` unterstützt bereits ein `CancellationToken`; ein „Abbrechen“-Button ist keine Anforderung. |
| 7 | Logeinträge liegen komplett im Speicher | akzeptiert bis M8 | Der `DateiLogLeser` liest Dateien zeilenweise, hält aber alle Einträge des Zeitraums im Speicher. Ob das für 1 Mio. Zeilen reicht (NFA11), zeigt der Performance-Test in M8. |
| 8 | Tabellenanzeige auf 10.000 Einträge begrenzt | akzeptiert | Die Ansicht „Logs und Fehler“ zeigt höchstens 10.000 Einträge gleichzeitig (Hinweis in der Zusammenfassung); Filter und Gruppen arbeiten auf allen Einträgen. |
