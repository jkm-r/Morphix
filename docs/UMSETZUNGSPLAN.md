# Morphix – Umsetzungsplan für Claude Code

Jeder Meilenstein endet mit: `dotnet build` ohne Warnungen, `dotnet test` grün, Commit, kurze
Zusammenfassung – dann anhalten. Priorität: alle Must-Anforderungen vor Should, Should vor Could.
Was nicht geschafft wird, kommt mit Begründung in `docs/TECHNICAL_DEBT.md`.

---

## M1 – Grundgerüst

- Solution und vier Projekte laut `CLAUDE.md` anlegen, Projektreferenzen setzen, Pakete einbinden.
- `Directory.Build.props` mit Nullable, ImplicitUsings, TreatWarningsAsErrors, LangVersion.
- `.gitignore` für Visual Studio/.NET, `README.md` (Kurzbeschreibung, Build, Start), leere
  `docs/TECHNICAL_DEBT.md`.
- `App.xaml.cs`: DI-Container aufbauen, `MainWindow` über DI erzeugen.
- `MainWindow`: Kopfleiste (Profilauswahl, Datum von/bis, „Analyse starten“, Hinweis „Demo-Modus“),
  Navigation links (Dashboard, Logs und Fehler, Dienste, Aufgaben, Bericht), Inhaltsbereich, Statusleiste.
  Navigation wechselt per ViewModel (`AktuelleAnsicht`) und DataTemplates zwischen leeren Ansichten.
- Ein Platzhaltertest, damit `dotnet test` läuft.

**Fertig, wenn:** Anwendung startet, Navigation funktioniert, Tests laufen.

## M2 – Domäne und Algorithmen (Morphix.Core)

- Geschäftsobjekte aus dem Datenmodell der Spezifikation als Klassen/Records: Systemprofil,
  Logquelle (inkl. optionalem `Zeilenmuster`), Dienst, GeplanteAufgabe, Aufgabenlauf, Diagnose,
  Logeintrag, Fehlergruppe, SupportBericht, Dienstaktion. Enums: `Schweregrad` (Info, Warnung,
  Fehler, Kritisch), `DienstStatus`, `Starttyp`, `LogquellenTyp` (Datei, Ereignisanzeige),
  `DienstAktionsart` (Starten, Stoppen, Neustarten).
- `ZeitraumRegel` (GR01) und `ProfilValidator` (GR02 + Feldregeln aus „Eingabevalidierung“ der
  Spezifikation), Rückgabe als Validierungsergebnis mit deutschen Meldungen exakt wie in der Spezifikation.
- `LogParser`:
  - Standardmuster: `^(?<zeit>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,3})?)\s+\[?(?<stufe>[A-Za-z]+)\]?\s+(?<text>.*)$`
  - Eigenes Muster je Logquelle möglich (benannte Gruppen `zeit`, `stufe`, `text`).
  - Stufen-Mapping (Groß-/Kleinschreibung egal): TRACE/DEBUG/INFO → Info; WARN/WARNING → Warnung;
    ERR/ERROR → Fehler; FATAL/CRIT/CRITICAL → Kritisch; Unbekannt → Info.
  - GR06: Zeilen ohne Zeitstempel an den vorherigen Eintrag anhängen; vor dem ersten Eintrag verwerfen.
- `FehlerGruppierer` (P3): nur Fehler/Kritisch; Normalisierung in dieser Reihenfolge durch `#`
  ersetzen: GUIDs, Datum/Uhrzeit, Windows-Pfade, IPv4-Adressen, übrige Zahlen. Gruppieren nach
  (Quelle, Muster), je Gruppe Anzahl, erstes/letztes Auftreten; absteigend nach Anzahl sortieren.
  Beispiel aus der Spezifikation muss als Test grün sein:
  „Timeout nach 30 s bei Terminal 12“ → „Timeout nach # s bei Terminal #“.
- `DatenMaskierer`: IPv4 → `***.***.***.***`; Benutzernamen in den Formen `DOMAENE\name`,
  `user=name`, `Benutzer: name` sowie der aktuelle `Environment.UserName` → `[Benutzer]`.
- Unit-Tests für alle Regeln und Algorithmen inkl. Randfällen (leere Eingabe, mehrzeilige Meldung,
  unbekannte Stufe, genau 30 Tage, Ende in der Zukunft).

**Fertig, wenn:** Core hat keine Windows-Abhängigkeit, alle Tests grün.

## M3 – Profile, Demo-Daten, Datenquellen

- Schnittstellen aus `CLAUDE.md` in `Morphix.Core/Schnittstellen` definieren.
- `JsonProfilRepository`: Profile unter `%AppData%\Morphix\Profile\*.json`; zuletzt genutztes
  Profil merken. Beim ersten Start Demo-Profil aus `demo/` anbieten (P1).
- Demo-Daten in `demo/` erzeugen (fiktiv, herstellerneutral, deutsch):
  - Demo-Profil „Demo – Zeiterfassung Musterwerk“ mit `istDemo: true`.
  - 4 Dienste: `ZE-Applikationsserver` (kritisch), `ZE-Terminalkommunikation` (kritisch),
    `ZE-Zutrittsdienst`, `ZE-Exportdienst`; einer davon im Demo-Zustand gestoppt.
  - 3 geplante Aufgaben: „Nächtlicher Lohnexport“ (fehlgeschlagen, Code 0x1), „Stammdatenabgleich“,
    „Logbereinigung“.
  - 3 Logdateien über 7 Tage mit zusammen ca. 3.000 Zeilen: überwiegend Info, realistische
    wiederkehrende Fehler (Terminal-Timeouts mit wechselnden Terminalnummern, verlorene
    Datenbankverbindung, fehlgeschlagener Export, Lizenzwarnung), mehrzeilige Stacktraces, einige
    IP-Adressen und Benutzernamen (fiktiv) zum Testen der Maskierung.
  - Ein kleines, im Repo enthaltenes Skript oder Testhelfer, das diese Dateien reproduzierbar
    (fester Seed) erzeugt.
- Demo-Adapter: `DemoLogLeser` liest die Demo-Dateien und verschiebt alle Zeitstempel so, dass der
  jüngste Eintrag „jetzt“ ist (damit der Standardzeitraum immer Treffer liefert).
  `DemoDienstGateway` hält Zustände im Speicher, simuliert Aktionen mit kurzer Verzögerung (GR04).
  `DemoAufgabenGateway`, `DemoEreignisLeser` liefern feste Beispieldaten.
- `WindowsDatenquellenFabrik.Erzeugen(profil)` wählt echte oder Demo-Adapter anhand `istDemo`.
- `ProfilService` + Dialog „Profil bearbeiten“ mit Validierung am Feld (rote Markierung,
  Speichern deaktiviert bis gültig).

**Fertig, wenn:** Profil anlegen/speichern/laden funktioniert (US01), Demo-Profil lädt beim ersten Start (US02).

## M4 – Analyse, Dashboard, Logs und Fehler

- `DateiLogLeser`: Dateien je Logquelle nach Dateimuster, Änderungsdatum im Zeitraum; lesend
  öffnen (GR05); UTF-8 mit Fallback auf Windows-1252; unlesbare/gesperrte Dateien überspringen und
  in der Diagnose melden (NFA07).
- `DiagnoseService.ErstellenAsync`: GR01 prüfen, Datenquellen über Fabrik, Logs parsen, Dienste und
  Aufgaben parallel abfragen (`Task.WhenAll`), Fehler gruppieren, Diagnose als abgeschlossen markieren.
  Fortschritt melden; Statusleiste zeigt Anzahl und Größe eingelesener Dateien (US03).
- Dashboard: Kennzahlen (Anzahl Fehler, gestoppte kritische Dienste, fehlgeschlagene Aufgaben),
  Top-Fehlergruppen.
- Ansicht „Logs und Fehler“: Filter Schweregrad, Suchbegriff (max. 200 Zeichen), Zeitraum;
  kombinierbar, sofort wirksam (US04); Tabelle der Fehlergruppen, Doppelklick zeigt Einträge (US05).
- Tests: DiagnoseService mit NSubstitute-Fakes; DateiLogLeser mit temporären Dateien (inkl.
  gesperrter und leerer Datei).

**Fertig, wenn:** Im Demo-Modus liefert „Analyse starten“ ein gefülltes Dashboard und filterbare Fehlergruppen.

## M5 – Dienste

- `WindowsDienstGateway` (ServiceController): Status und Starttyp abfragen; Start/Stopp/Neustart
  mit `WaitForStatus` und Timeout 30 s (P5). Nicht vorhandener Dienst → Status „Nicht gefunden“.
- `WindowsRechtePruefer` (WindowsPrincipal, Rolle Administrator).
- `DienstService`: GR03 vollständig prüfen (Profil, Rechte, Bestätigung als Parameter), Ergebnis zurückgeben.
- Ansicht „Dienste“: Tabelle mit Status und Starttyp, Aktualisierung alle 5 s (`PeriodicTimer`),
  Schaltflächen ohne Adminrechte deaktiviert mit Hinweis in der Statusleiste, Bestätigungsdialog
  „Dienst X wirklich stoppen?“ (US07, US08).
- Should US09: gestoppter kritischer Dienst farblich hervorgehoben + Hinweis.

**Fertig, wenn:** Im Demo-Modus lassen sich Dienste steuern; ohne Adminrechte bleibt die Ansicht lesend.

## M6 – Support-Bericht und Export

- `BerichtService.Erzeugen(diagnose, maskieren)`: GR09 prüfen; Inhalt: Systeminfo (Rechnername
  maskierbar, Windows-Version, Zeitpunkt, Profil, Zeitraum), Dienststatus, Fehlerübersicht (Gruppen),
  übersprungene Dateien; Ereignisse und Aufgaben nur, wenn vorhanden (US13).
- HTML: eine selbstständige Datei mit eingebettetem CSS, deutsch, druckfreundlich; alle Inhalte
  HTML-kodiert.
- Maskierung standardmäßig an (GR10, US15).
- `HtmlZipExporter`: ZIP mit `bericht.html` und Ordner `logs/` (Kopien der gelesenen Dateien,
  Originale unangetastet) am gewählten Ort (US14). Dateiname-Vorschlag
  `Morphix_<Profil>_<yyyyMMdd_HHmm>.zip`.
- Ansicht „Bericht“: Vorschau (WebBrowser-Control oder FlowDocument), Option „Sensible Daten
  maskieren“, Schaltfläche „Als ZIP exportieren“ mit Speichern-Dialog; Prüfung Ordner beschreibbar.

**Fertig, wenn:** Ein Bericht lässt sich im Demo-Modus in ≤ 4 Schritten erzeugen und als ZIP exportieren.
→ **Damit sind alle Must-Anforderungen umgesetzt.** Commit mit Tag `v0.9-must`.

## M7 – Should-Anforderungen

- `WindowsEreignisLeser` (US06): Protokolle Application und System im Zeitraum, nur Fehler/Kritisch
  und Warnungen; erscheinen in derselben Liste wie Logeinträge (Quelle „Ereignisanzeige“).
- `TaskSchedulerGateway` (US10): Profilaufgaben mit letztem Lauf, Ergebnis, nächstem Lauf.
- Ansicht „Aufgaben“; fehlgeschlagene (GR08) rot markiert (US11).

## M8 – Qualität und Abschluss

- Performance-Test (NFA01/NFA11): Generator für 100.000 bzw. 1.000.000 Zeilen; Laufzeit und
  Speicher messen und ausgeben (als separat startbarer Test, `[Trait("Kategorie","Performance")]`).
- Robustheitstest NFA07 mit Ordner aus 10 fehlerhaften Dateien (leer, binär, gesperrt, falsche
  Kodierung, ohne Zeitstempel).
- Integritätstest NFA08: Änderungsdatum und SHA-256 der Testdateien vor/nach Analyse gleich.
- Abdeckung messen, Ziel ≥ 70 % für Morphix.Core (NFA10); Lücken schließen.
- README um Benutzeranleitung ergänzen (Voraussetzungen .NET 10 Desktop Runtime, Start, Demo-Modus,
  Profil anlegen, Bericht exportieren).
- `dotnet publish` (framework-dependent, win-x64) in Ordner `release/`.

## Optional (Could)

- US12 Diagramm Fehler pro Stunde/Tag mit OxyPlot im Dashboard.
- US16 Protokoll der Dienstaktionen (JSON-Lines unter %AppData%\Morphix).
