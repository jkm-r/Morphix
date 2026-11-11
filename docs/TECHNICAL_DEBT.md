# Technische Schulden und offene Punkte

Hier steht, was bewusst später oder anders umgesetzt wird, mit Begründung.

| Nr. | Thema | Stand | Begründung / Plan |
|---|---|---|---|
| 1 | `WindowsDatenquellenFabrik` wirft für nicht-Demo-Profile `NotSupportedException` | offen bis M4–M7 | Die echten Windows-Adapter (`DateiLogLeser`, `WindowsDienstGateway`, `TaskSchedulerGateway`, `WindowsEreignisLeser`) sind erst in M4, M5 und M7 vorgesehen. Bis dahin sind nur Demo-Profile analysierbar. Die Oberfläche kann echte Profile aber schon anlegen und speichern (US01). |
| 2 | Tageslogik des Zeitraums (GR01) in der Kopfleiste | offen bis M4 | `ZeitraumRegel` rechnet mit Zeitpunkten, die Kopfleiste liefert Datumswerte. Wie „bis“ (Tagesende, aber nicht in der Zukunft) und „30 Tage“ bei Kalendertagen zusammenspielen, wird mit „Analyse starten“ in M4 entschieden und dort getestet. Bis dahin ist „Analyse starten“ ein Platzhalter. |
| 3 | Defekte Profildateien werden still übersprungen | akzeptiert | `JsonProfilRepository` ignoriert unlesbare JSON-Dateien (NFA07), damit ein kaputtes Profil nicht alle anderen blockiert. Es gibt noch keine Anzeige, welche Datei übersprungen wurde. Ggf. in M8 über die Statusleiste melden. |
| 4 | Profile löschen ist nur im Repository, nicht in der Oberfläche | akzeptiert | Der Umsetzungsplan verlangt für M3 Anlegen, Speichern, Laden (US01). Löschen steht nicht in den Anforderungen. |
| 5 | Ordnerprüfung zur Eingabezeit | akzeptiert | `ProfilValidator` prüft die Existenz des Logordners bei jeder Änderung am Feld (Dateizugriff im UI-Thread, bei Netzlaufwerken ggf. träge). Für lokale Ordner unkritisch; bei Bedarf entkoppeln (Debounce/async). |
| 6 | Demo-Logleser liest ganze Dateien in den Speicher | akzeptiert | Die Demo-Dateien sind klein (rund 3.000 Zeilen). Der echte `DateiLogLeser` in M4 liest zeilenweise streamend. |
