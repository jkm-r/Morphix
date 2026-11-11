# Morphix – Architektur im Ist-Zustand

Stand: Version 1.1 (Tag `v1.1`, Commit `c198e9d`). Dieses Dokument beschreibt, wie Morphix **tatsächlich** gebaut ist. Es
ergänzt das Architekturdokument (`docs/Morphix – Architekturdokument (Entwurf).pdf`), ersetzt dessen Entwurfsdiagramme
(`schichten.png`, `klassen.png`, `sequenz.png` liegen nicht im Repository) und listet in Abschnitt 4 alle Abweichungen. Bei
Widersprüchen zwischen Dokumenten gilt weiterhin die Regel aus `CLAUDE.md`: Die Dokumente in `docs/` haben Vorrang vor
`CLAUDE.md`; bewusste Abweichungen stehen zusätzlich in `docs/AENDERUNGEN.md`, offene Punkte in `docs/TECHNICAL_DEBT.md`.

Lesehilfe zu den Diagrammen: Typen mit `?` (Nullable) sind ohne Fragezeichen geschrieben, `Task~T~` bedeutet `Task<T>`.
Es sind jeweils die wichtigsten Attribute und Methoden aufgeführt, nicht alle. Namen sind wie im Code (deutsch, ohne Umlaute).

---

## 1. Klassendiagramme je Schicht

### 1.1 Schichten und Abhängigkeiten

```mermaid
flowchart TB
    subgraph Prasentation["Präsentation – Morphix.App (net10.0-windows, WPF)"]
        App["Views, ViewModels, Dialoge<br/>App.xaml.cs = Composition Root (DI)"]
    end
    subgraph Core["Morphix.Core (net10.0, ohne Windows-Bezug)"]
        Anwendung["Anwendung<br/>DiagnoseService, DienstService,<br/>BerichtService, ProfilService"]
        Domaene["Domäne<br/>Geschäftsobjekte, Regeln, Algorithmen"]
        Ports["Schnittstellen (Ports)<br/>ILogLeser, IDienstGateway, ..."]
    end
    subgraph Infra["Infrastruktur – Morphix.Infrastructure (net10.0-windows)"]
        Adapter["Windows-Adapter, Demo-Adapter,<br/>JSON, ZIP"]
    end
    Tests["Morphix.Tests<br/>(xUnit, NSubstitute)"]

    App -->|"ViewModels nutzen"| Anwendung
    App -->|"ViewModels nutzen auch direkt"| Domaene
    App -.->|"nur App.xaml.cs: DI-Registrierung"| Adapter
    Anwendung --> Domaene
    Anwendung --> Ports
    Ports --> Domaene
    Adapter -->|"implementiert"| Ports
    Adapter --> Domaene
    Tests -.-> App
    Tests -.-> Core
    Tests -.-> Adapter
```

### 1.2 Domäne (Morphix.Core/Domaene)

```mermaid
classDiagram
    direction LR

    class Systemprofil {
        <<record>>
        +string Name
        +string Beschreibung
        +bool IstDemo
        +List~Logquelle~ Logquellen
        +List~Dienst~ Dienste
        +List~GeplanteAufgabe~ Aufgaben
    }
    class Logquelle {
        <<record>>
        +string Pfad
        +string Dateimuster
        +LogquellenTyp Typ
        +string Zeilenmuster
    }
    class Dienst {
        <<record>>
        +string Dienstname
        +string Anzeigename
        +DienstStatus Status
        +Starttyp Starttyp
        +bool IstKritisch
    }
    class GeplanteAufgabe {
        <<record>>
        +string Name
        +DateTime LetzterLauf
        +int LetztesErgebnis
        +DateTime NaechsterLauf
        +bool IstFehlgeschlagen
    }
    class Diagnose {
        <<record>>
        +Systemprofil Profil
        +DateTime Von
        +DateTime Bis
        +DateTime ErstelltAm
        +IReadOnlyList~Logeintrag~ Logeintraege
        +IReadOnlyList~Fehlergruppe~ Fehlergruppen
        +IReadOnlyList~Dienst~ Dienste
        +IReadOnlyList~GeplanteAufgabe~ Aufgaben
        +IReadOnlyList~string~ GeleseneDateien
        +IReadOnlyList~UebersprungeneDatei~ UebersprungeneDateien
        +long GeleseneBytes
        +bool IstAbgeschlossen
    }
    class Logeintrag {
        <<record>>
        +DateTime Zeitstempel
        +Schweregrad Schweregrad
        +string Quelle
        +string Meldung
        +string EreignisQuelle$
    }
    class Fehlergruppe {
        <<record>>
        +string Meldungsmuster
        +string Quelle
        +int Anzahl
        +DateTime ErstesAuftreten
        +DateTime LetztesAuftreten
        +IReadOnlyList~Logeintrag~ Eintraege
    }
    class Dienstaktion {
        <<record>>
        +DateTime Zeitpunkt
        +DienstAktionsart Aktion
        +string Benutzer
        +bool Erfolgreich
        +string Dienstname
        +string Profil
        +bool Simuliert
        +string Meldung
    }
    class SupportBericht {
        <<record>>
        +DateTime ErstelltAm
        +bool Maskiert
        +string Dateipfad
    }
    class Aufgabenlauf {
        <<record>>
        +DateTime Start
        +DateTime Ende
        +int Ergebniscode
    }
    class LogleseErgebnis {
        <<record>>
        +IReadOnlyList~Logeintrag~ Eintraege
        +int Dateianzahl
        +long Bytes
        +IReadOnlyList~UebersprungeneDatei~ Uebersprungen
        +IReadOnlyList~string~ GeleseneDateien
    }
    class FehlerVerlauf {
        <<record>>
        +Zeitaufloesung Aufloesung
        +IReadOnlyList~VerlaufPunkt~ Punkte
        +Berechne(eintraege, von, bis) FehlerVerlauf$
    }
    class Validierungsergebnis {
        <<record>>
        +IReadOnlyList~Validierungsfehler~ Fehler
        +bool IstGueltig
        +MeldungenFuer(feld) IEnumerable~string~
    }

    class LogParser {
        +string Standardmuster$
        +LogParser(zeilenmuster)
        +Parse(zeilen, quelle) IEnumerable~Logeintrag~
        +ZuSchweregrad(stufe) Schweregrad$
    }
    class FehlerGruppierer {
        <<static>>
        +Normalisiere(meldung) string
        +Gruppiere(eintraege) IReadOnlyList~Fehlergruppe~
    }
    class DatenMaskierer {
        +string IpErsatz$
        +string BenutzerErsatz$
        +DatenMaskierer(aktuellerBenutzer)
        +Maskiere(text) string
    }
    class ZeitraumRegel {
        <<static>>
        +TimeSpan MaximaleDauer$
        +AusTagen(vonTag, bisTag, jetzt) Tupel
        +Pruefen(von, bis, jetzt) Validierungsergebnis
    }
    class ProfilValidator {
        +ProfilValidator(ordnerIstLesbar)
        +Pruefen(profil, namenAndererProfile) Validierungsergebnis
    }
    class LogFilter {
        <<static>>
        +Anwenden(eintraege, schweregrad, suchbegriff, vonTag, bisTag) IEnumerable~Logeintrag~
        +PruefeSuchbegriff(suchbegriff) Validierungsergebnis
    }

    class Schweregrad {
        <<enumeration>>
        Info
        Warnung
        Fehler
        Kritisch
    }
    class DienstStatus {
        <<enumeration>>
        Unbekannt
        Gestartet
        Gestoppt
        Startet
        Stoppt
        Angehalten
        NichtGefunden
    }
    class Starttyp {
        <<enumeration>>
        Unbekannt
        Automatisch
        Manuell
        Deaktiviert
    }
    class LogquellenTyp {
        <<enumeration>>
        Datei
        Ereignisanzeige
    }
    class DienstAktionsart {
        <<enumeration>>
        Starten
        Stoppen
        Neustarten
    }

    Systemprofil "1" *-- "1..*" Logquelle
    Systemprofil "1" *-- "1..*" Dienst
    Systemprofil "1" *-- "0..*" GeplanteAufgabe
    Diagnose --> Systemprofil : Profil
    Diagnose "1" o-- "0..*" Logeintrag
    Diagnose "1" o-- "0..*" Fehlergruppe
    Fehlergruppe "1" o-- "1..*" Logeintrag
    Logquelle --> LogquellenTyp
    Dienst --> DienstStatus
    Dienst --> Starttyp
    Logeintrag --> Schweregrad
    Dienstaktion --> DienstAktionsart
    LogParser ..> Logeintrag : erzeugt
    FehlerGruppierer ..> Fehlergruppe : erzeugt
    FehlerVerlauf ..> Logeintrag : zählt
    ProfilValidator ..> Systemprofil : prüft GR02
    ZeitraumRegel ..> Validierungsergebnis : prüft GR01
    LogFilter ..> Logeintrag : filtert
    DatenMaskierer ..> Logeintrag : maskiert Texte
```

### 1.3 Schnittstellen (Ports) und Anwendung (Morphix.Core/Schnittstellen und Anwendung)

```mermaid
classDiagram
    direction LR

    class ILogLeser {
        <<interface>>
        +LesenAsync(quelle, von, bis, fortschritt, abbruch) Task~LogleseErgebnis~
    }
    class IEreignisLeser {
        <<interface>>
        +LesenAsync(quelle, von, bis, abbruch) Task~IReadOnlyList~Logeintrag~~
    }
    class IDienstGateway {
        <<interface>>
        +AbfragenAsync(dienste, abbruch) Task~IReadOnlyList~Dienst~~
        +AusfuehrenAsync(dienst, aktion, abbruch) Task~Dienst~
    }
    class IAufgabenGateway {
        <<interface>>
        +AbfragenAsync(aufgaben, abbruch) Task~IReadOnlyList~GeplanteAufgabe~~
    }
    class IRechtePruefer {
        <<interface>>
        +HatAdminrechte() bool
    }
    class IDatenquellenFabrik {
        <<interface>>
        +Erzeugen(profil) Datenquellen
    }
    class Datenquellen {
        <<record>>
        +ILogLeser LogLeser
        +IDienstGateway Dienste
        +IAufgabenGateway Aufgaben
        +IEreignisLeser Ereignisse
        +IRechtePruefer Rechte
    }
    class IProfilRepository {
        <<interface>>
        +LadenAlleAsync(abbruch) Task~IReadOnlyList~Systemprofil~~
        +SpeichernAsync(profil, alterName, abbruch) Task
        +LoeschenAsync(name, abbruch) Task
        +LadeZuletztGenutztesAsync(abbruch) Task~string~
        +MerkeZuletztGenutztesAsync(name, abbruch) Task
        +LadeDemoProfilAsync(abbruch) Task~Systemprofil~
    }
    class IBerichtExporter {
        <<interface>>
        +KannSchreiben(zielpfad) bool
        +ExportierenAsync(html, logdateien, zielpfad, zeilenFilter, abbruch) Task
    }
    class IAktionsProtokoll {
        <<interface>>
        +ProtokolliereAsync(aktion, abbruch) Task
        +LeseAsync(maximal, abbruch) Task~IReadOnlyList~Dienstaktion~~
    }

    class DiagnoseService {
        <<service>>
        +DiagnoseService(fabrik, zeit)
        +DateTime Jetzt
        +PruefeTage(vonTag, bisTag) Tupel
        +ErstellenAsync(profil, von, bis, fortschritt, abbruch) Task~Diagnose~
    }
    class DienstService {
        <<service>>
        +DienstService(fabrik, protokoll, zeit, benutzer)
        +AbfragenAsync(profil, abbruch) Task~IReadOnlyList~Dienst~~
        +HatAdminrechte(profil) bool
        +AusfuehrenAsync(profil, dienst, aktion, bestaetigt, abbruch) Task~DienstAktionsErgebnis~
        +ProtokollLesenAsync(maximal, abbruch) Task~IReadOnlyList~Dienstaktion~~
    }
    class BerichtService {
        <<service>>
        +BerichtService(exporter, zeit, system, maskierer)
        +Erzeugen(diagnose, maskieren) string
        +Dateinamenvorschlag(diagnose) string
        +PruefeZielpfad(zielpfad) string
        +ExportierenAsync(diagnose, maskieren, zielpfad, abbruch) Task~SupportBericht~
    }
    class ProfilService {
        <<service>>
        +ProfilService(repository, validator)
        +IReadOnlyList~Systemprofil~ Profile
        +StartenAsync(abbruch) Task~ProfilStart~
        +Pruefen(profil, bisherigerName) Validierungsergebnis
        +SpeichernAsync(profil, bisherigerName, abbruch) Task~Validierungsergebnis~
        +MerkeAktivAsync(profil, abbruch) Task
    }
    class HtmlBerichtErzeuger {
        <<internal>>
        +Erzeugen() string
    }
    class DienstAktionsErgebnis {
        <<record>>
        +bool Erfolgreich
        +string Meldung
        +Dienst Dienst
    }

    Datenquellen o-- ILogLeser
    Datenquellen o-- IDienstGateway
    Datenquellen o-- IAufgabenGateway
    Datenquellen o-- IEreignisLeser
    Datenquellen o-- IRechtePruefer
    IDatenquellenFabrik ..> Datenquellen : liefert
    DiagnoseService --> IDatenquellenFabrik
    DiagnoseService ..> FehlerGruppierer : Task.Run
    DiagnoseService ..> ZeitraumRegel : GR01
    DienstService --> IDatenquellenFabrik
    DienstService --> IAktionsProtokoll
    DienstService ..> DienstAktionsErgebnis
    BerichtService --> IBerichtExporter
    BerichtService --> DatenMaskierer
    BerichtService ..> HtmlBerichtErzeuger : GR09, GR10
    ProfilService --> IProfilRepository
    ProfilService --> ProfilValidator
```

### 1.4 Infrastruktur (Morphix.Infrastructure)

```mermaid
classDiagram
    direction TB

    class WindowsDatenquellenFabrik {
        +WindowsDatenquellenFabrik(demoOrdner, windowsDienste, windowsRechte)
        +Erzeugen(profil) Datenquellen
    }

    class DateiLogLeser {
        +LesenAsync(quelle, von, bis, fortschritt, abbruch) Task~LogleseErgebnis~
    }
    class DemoLogLeser {
        +DemoLogLeser(demoOrdner, jetzt)
        +LesenAsync(quelle, von, bis, fortschritt, abbruch) Task~LogleseErgebnis~
    }

    class WindowsDienstGateway {
        +TimeSpan StandardTimeout$
        +WindowsDienstGateway(zugriff, timeout)
        +AbfragenAsync(dienste, abbruch) Task~IReadOnlyList~Dienst~~
        +AusfuehrenAsync(dienst, aktion, abbruch) Task~Dienst~
    }
    class IDienstZugriff {
        <<interface>>
        +Oeffnen(dienstname) IDienstHandle
    }
    class ServiceControllerZugriff
    class DemoDienstGateway {
        +DemoDienstGateway(dienste, verzoegerung)
    }

    class WindowsAufgabenGateway {
        +AbfragenAsync(aufgaben, abbruch) Task~IReadOnlyList~GeplanteAufgabe~~
    }
    class IAufgabenZugriff {
        <<interface>>
        +Lesen(aufgabenname) AufgabenZustand
    }
    class TaskSchedulerZugriff
    class DemoAufgabenGateway

    class WindowsEreignisLeser {
        +int MaxEreignisse$
        +LesenAsync(quelle, von, bis, abbruch) Task~IReadOnlyList~Logeintrag~~
    }
    class IEreignisZugriff {
        <<interface>>
        +Lesen(protokoll, von, bis, maximal, abbruch) IReadOnlyList~RohEreignis~
    }
    class EventLogZugriff
    class DemoEreignisLeser

    class WindowsRechtePruefer {
        +HatAdminrechte() bool
    }
    class DemoRechtePruefer {
        +HatAdminrechte() bool
    }

    class JsonProfilRepository {
        +JsonProfilRepository(profilOrdner, einstellungsDatei, demoProfilDatei)
    }
    class HtmlZipExporter {
        +KannSchreiben(zielpfad) bool
        +ExportierenAsync(html, logdateien, zielpfad, zeilenFilter, abbruch) Task
    }
    class JsonLinesAktionsProtokoll {
        +JsonLinesAktionsProtokoll(pfad)
        +ProtokolliereAsync(aktion, abbruch) Task
        +LeseAsync(maximal, abbruch) Task~IReadOnlyList~Dienstaktion~~
    }

    ILogLeser <|.. DateiLogLeser
    ILogLeser <|.. DemoLogLeser
    DemoLogLeser ..> DateiLogLeser : liest, verschiebt Zeitstempel
    IDienstGateway <|.. WindowsDienstGateway
    IDienstGateway <|.. DemoDienstGateway
    WindowsDienstGateway --> IDienstZugriff
    IDienstZugriff <|.. ServiceControllerZugriff
    IAufgabenGateway <|.. WindowsAufgabenGateway
    IAufgabenGateway <|.. DemoAufgabenGateway
    WindowsAufgabenGateway --> IAufgabenZugriff
    IAufgabenZugriff <|.. TaskSchedulerZugriff
    IEreignisLeser <|.. WindowsEreignisLeser
    IEreignisLeser <|.. DemoEreignisLeser
    WindowsEreignisLeser --> IEreignisZugriff
    IEreignisZugriff <|.. EventLogZugriff
    IRechtePruefer <|.. WindowsRechtePruefer
    IRechtePruefer <|.. DemoRechtePruefer
    IProfilRepository <|.. JsonProfilRepository
    IBerichtExporter <|.. HtmlZipExporter
    IAktionsProtokoll <|.. JsonLinesAktionsProtokoll
    IDatenquellenFabrik <|.. WindowsDatenquellenFabrik
    WindowsDatenquellenFabrik ..> DateiLogLeser : echtes Profil
    WindowsDatenquellenFabrik ..> DemoLogLeser : Demo-Profil
    WindowsDatenquellenFabrik ..> WindowsDienstGateway
    WindowsDatenquellenFabrik ..> DemoDienstGateway : je Profil gemerkt
```

Die Zugriffsschnittstellen `IDienstZugriff`, `IAufgabenZugriff` und `IEreignisZugriff` sind eine zusätzliche Nahtstelle *innerhalb*
der Infrastruktur: Sie kapseln `ServiceController`, TaskScheduler und `EventLogReader`, damit die Logik der Adapter ohne echtes
System mit Fakes getestet werden kann. Die Zugriffsklassen selbst (`ServiceControllerZugriff`, `TaskSchedulerZugriff`,
`EventLogZugriff`) sind nicht automatisch getestet.

### 1.5 Präsentation (Morphix.App)

```mermaid
classDiagram
    direction LR

    class App {
        +BaueContainer(datenOrdner, demoOrdner) ServiceProvider$
        #OnStartup(e)
    }
    class MainViewModel {
        +ObservableCollection~Systemprofil~ Profile
        +IReadOnlyList~NavigationsEintrag~ Navigation
        +Systemprofil AusgewaehltesProfil
        +DateTime ZeitraumVon
        +DateTime ZeitraumBis
        +string ZeitraumFehler
        +string Statusmeldung
        +int Fortschritt
        +bool IstAnalyseLaeuft
        +ObservableViewModel AktuelleAnsicht
        +InitialisierenAsync() Task
        +AnalyseStartenCommand
        +ProfilNeuCommand
        +ProfilBearbeitenCommand
    }
    class DashboardViewModel {
        +string FehlerAnzahl
        +string GestoppteKritischeDienste
        +string FehlgeschlageneAufgaben
        +ObservableCollection~Fehlergruppe~ HaeufigsteGruppen
        +PlotModel Diagramm
        +Zeige(diagnose)
        +BerichtErstellenCommand
    }
    class LogsUndFehlerViewModel {
        +SchweregradAuswahl AusgewaehlterSchweregrad
        +string Suchbegriff
        +DateTime VonTag
        +DateTime BisTag
        +IReadOnlyList~Fehlergruppe~ Gruppen
        +IReadOnlyList~Logeintrag~ Eintraege
        +Zeige(diagnose)
        +GruppeOeffnenCommand
    }
    class DiensteViewModel {
        +IReadOnlyList~DienstAnzeige~ Dienste
        +DienstAnzeige AusgewaehlterDienst
        +bool HatAdminrechte
        +IReadOnlyList~AktionAnzeige~ Protokoll
        +bool IstAktiv
        +SetzeProfil(profil)
        +AktualisierenAsync() Task
        +AktualisiereProtokollAsync() Task
        +StartenCommand
        +StoppenCommand
        +NeustartenCommand
    }
    class AufgabenViewModel {
        +ObservableCollection~AufgabeAnzeige~ Aufgaben
        +string FehlerHinweis
        +Zeige(diagnose)
    }
    class BerichtViewModel {
        +bool Maskieren
        +string Vorschau
        +bool HatDiagnose
        +string Meldung
        +Zeige(diagnose)
        +ExportierenCommand
        +ImBrowserOeffnenCommand
    }
    class ProfilBearbeitenViewModel {
        +string Name
        +ObservableCollection~LogquelleZeile~ Logquellen
        +ObservableCollection~DienstZeile~ Dienste
        +ObservableCollection~AufgabeZeile~ Aufgaben
        +bool IstGueltig
        +SpeichernCommand
    }
    class VerlaufDiagramm {
        <<static>>
        +Erzeuge(verlauf) PlotModel
    }
    class IBestaetigungsDialog {
        <<interface>>
        +Bestaetigen(titel, frage) bool
    }
    class IExportDialog {
        <<interface>>
        +ZielWaehlen(vorgeschlagenerName) string
    }
    class IProfilDialogService {
        <<interface>>
        +Bearbeiten(profil) Systemprofil
    }
    class IBrowserOeffner {
        <<interface>>
        +ZeigeHtml(html) string
    }
    class WebBrowserHtml {
        <<attached>>
        +Html
    }

    MainViewModel --> DiagnoseService
    MainViewModel --> ProfilService
    MainViewModel --> IProfilDialogService
    MainViewModel o-- DashboardViewModel
    MainViewModel o-- LogsUndFehlerViewModel
    MainViewModel o-- DiensteViewModel
    MainViewModel o-- AufgabenViewModel
    MainViewModel o-- BerichtViewModel
    DashboardViewModel ..> VerlaufDiagramm
    DashboardViewModel ..> FehlerVerlauf
    LogsUndFehlerViewModel ..> LogFilter
    LogsUndFehlerViewModel ..> FehlerGruppierer
    DiensteViewModel --> DienstService
    DiensteViewModel --> IBestaetigungsDialog
    BerichtViewModel --> BerichtService
    BerichtViewModel --> IExportDialog
    BerichtViewModel --> IBestaetigungsDialog
    BerichtViewModel --> IBrowserOeffner
    ProfilBearbeitenViewModel --> ProfilService
    IProfilDialogService ..> ProfilBearbeitenViewModel : öffnet
    App ..> MainViewModel : DI
```

Alle ViewModels erben von `ObservableViewModel` (CommunityToolkit.Mvvm `ObservableObject`). Die DataTemplates in `App.xaml` ordnen
jedem ViewModel-Typ seine View zu; die Navigation tauscht nur das ViewModel (`MainViewModel.AktuelleAnsicht`). Die Views haben keine
Logik im Code-Behind; die einzige Ausnahme ist die Attached Property `WebBrowserHtml.Html` (reines View-Verhalten).

---

## 2. Sequenzdiagramm: „Analyse starten“ bis „Als ZIP exportieren“

Der Ablauf besteht aus zwei Phasen, die der Anwender nacheinander auslöst: die Analyse (Klick auf „Analyse starten“) und der Export aus der
Ansicht „Bericht“. Die Berichtsvorschau entsteht automatisch am Ende der Analyse. Die Dienste-Aktionen (Start, Stopp, Neustart) gehören nicht zu diesem Ablauf.

```mermaid
sequenceDiagram
    autonumber
    actor A as Anwender
    participant MVM as MainViewModel
    participant DS as DiagnoseService
    participant F as IDatenquellenFabrik<br/>(WindowsDatenquellenFabrik)
    participant LL as ILogLeser<br/>(DateiLogLeser / DemoLogLeser)
    participant EL as IEreignisLeser
    participant DG as IDienstGateway
    participant AG as IAufgabenGateway
    participant VMs as Dashboard-, Logs-, Aufgaben-<br/>ViewModel
    participant BVM as BerichtViewModel
    participant BS as BerichtService
    participant HE as HtmlBerichtErzeuger<br/>+ DatenMaskierer
    participant X as IExportDialog
    participant EX as IBerichtExporter<br/>(HtmlZipExporter)

    Note over A,MVM: Phase 1 – Analyse
    A->>MVM: Analyse starten (AnalyseStartenCommand)
    MVM->>DS: PruefeTage(von, bis)
    DS-->>MVM: Zeitraum 00:00:00 bis 23:59:59 (höchstens jetzt), GR01 geprüft
    MVM->>MVM: IstAnalyseLaeuft = true, Fortschritt = 0
    MVM->>DS: ErstellenAsync(profil, von, bis, IProgress)
    DS->>DS: ZeitraumRegel.Pruefen (GR01), sonst ArgumentException
    DS->>F: Erzeugen(profil)
    F-->>DS: Datenquellen (echt oder Demo, je nach IstDemo)

    par Logquellen (je Quelle parallel)
        alt Typ Datei
            DS->>LL: LesenAsync(quelle, von, bis, IProgress)
            LL-->>DS: LogleseErgebnis (Einträge, gelesene und übersprungene Dateien)
        else Typ Ereignisanzeige
            DS->>EL: LesenAsync(quelle, von, bis)
            EL-->>DS: Logeintraege mit Quelle "Ereignisanzeige"<br/>(bei Fehler: als übersprungen gemeldet)
        end
    and Dienststatus
        DS->>DG: AbfragenAsync(profil.Dienste)
        DG-->>DS: Dienste mit Status und Starttyp
    and Aufgaben
        DS->>AG: AbfragenAsync(profil.Aufgaben)
        AG-->>DS: Aufgaben mit letztem Lauf, Ergebnis, nächstem Lauf
    end

    DS->>DS: Einträge nach Zeit sortieren
    DS->>DS: Task.Run: FehlerGruppierer.Gruppiere (GR07)
    DS-->>MVM: Diagnose (IstAbgeschlossen = true)
    MVM->>VMs: Zeige(diagnose) – Dashboard (inkl. Diagramm), Logs und Fehler, Aufgaben
    MVM->>BVM: Zeige(diagnose), Maskierung wieder an (GR10)
    BVM->>BS: Task.Run: Erzeugen(diagnose, maskieren = true)
    BS->>BS: GR09 prüfen (Diagnose abgeschlossen)
    BS->>HE: HTML erzeugen, jeden Text maskieren, dann HtmlEncode
    HE-->>BS: HTML
    BS-->>BVM: HTML
    BVM-->>A: Vorschau im WebBrowser, Statusleiste: Analyse abgeschlossen
    MVM->>MVM: IstAnalyseLaeuft = false

    Note over A,EX: Phase 2 – Bericht prüfen und exportieren
    A->>BVM: Navigation „Bericht“ (optional: Maskierung ausschalten, nach Bestätigungsdialog)
    A->>BVM: Als ZIP exportieren (ExportierenCommand)
    BVM->>BS: Dateinamenvorschlag(diagnose)
    BS-->>BVM: Morphix_Profil_yyyyMMdd_HHmm.zip
    BVM->>X: ZielWaehlen(vorgeschlagenerName)
    X-->>BVM: Zielpfad (oder null bei Abbruch: Ende)
    BVM->>BS: PruefeZielpfad(zielpfad)
    BS->>EX: KannSchreiben(zielpfad)
    EX-->>BS: true / false
    alt Ordner nicht beschreibbar
        BS-->>BVM: "In diesen Ordner kann nicht geschrieben werden."
        BVM-->>A: Fehlermeldung, Ende
    else Ordner beschreibbar
        BVM->>BS: ExportierenAsync(diagnose, maskieren, zielpfad)
        BS->>BS: Erzeugen(diagnose, maskieren) – GR09, HTML
        BS->>EX: ExportierenAsync(html, GeleseneDateien, zielpfad, zeilenFilter)
        Note right of EX: zeilenFilter = DatenMaskierer.Maskiere,<br/>nur wenn Maskierung an, sonst null
        EX->>EX: ZIP anlegen: bericht.html
        loop je gelesene Logdatei
            EX->>EX: nur lesend öffnen (FileShare.ReadWrite), Kopie nach logs/<br/>(zeilenweise maskiert oder byte-genau)
        end
        EX->>EX: nicht lesbare Dateien in logs/_nicht_kopiert.txt vermerken
        EX-->>BS: fertig (bei Fehler: halbe ZIP-Datei wird gelöscht)
        BS-->>BVM: SupportBericht (ErstelltAm, Maskiert, Dateipfad)
        BVM-->>A: "Exportiert nach …"
    end
```

Kernpunkte des tatsächlichen Ablaufs:

- Logquellen, Dienststatus und Aufgaben werden **parallel** erfasst (`Task.WhenAll`); Ereignisprotokolle sind Logquellen vom Typ
  Ereignisanzeige und laufen im selben Zweig. Ein nicht lesbares Ereignisprotokoll bricht die Analyse nicht ab.
- Die Fabrik entscheidet über echte oder Demo-Adapter; `DiagnoseService` kennt den Unterschied nicht.
- Die Maskierung wirkt an zwei Stellen: im HTML (`HtmlBerichtErzeuger`, vor der Kodierung) und über den Zeilenfilter beim Kopieren
  der Logdateien ins ZIP. Beides geht von `BerichtService` aus; die Originaldateien werden nur gelesen (GR05).
- Die Vorschau wird mit derselben Methode erzeugt wie der Export (`BerichtService.Erzeugen`), beim Export aber neu, damit Vorschau
  und Datei zur gewählten Maskierung passen.

---

## 3. Verwendete NuGet-Pakete

Alle Versionen stammen aus den `.csproj`-Dateien, die Lizenzen aus den `.nuspec`-Metadaten der Pakete im NuGet-Cache.
`Directory.Build.props` setzt für alle Projekte C# 14, `Nullable`, `ImplicitUsings` und `TreatWarningsAsErrors`.

### 3.1 Direkt eingebundene Pakete

| Paket | Version | Lizenz | Projekt | Einsatz |
|---|---|---|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | MIT | Morphix.App | `ObservableObject`, `[ObservableProperty]`, `[RelayCommand]` |
| Microsoft.Extensions.DependencyInjection | 11.0.0 | MIT | Morphix.App | Dependency Injection in `App.xaml.cs` |
| OxyPlot.Wpf | 2.2.0 | MIT | Morphix.App | Fehlerdiagramm im Dashboard (US12) |
| System.Diagnostics.EventLog | 11.0.0 | MIT | Morphix.Infrastructure | Ereignisanzeige lesen (`EventLogReader`, US06) |
| System.ServiceProcess.ServiceController | 11.0.0 | MIT | Morphix.Infrastructure | Windows-Dienste abfragen und steuern |
| TaskScheduler | 2.12.2 | MIT | Morphix.Infrastructure | Aufgabenplanung lesen (US10) |
| Microsoft.NET.Test.Sdk | 18.11.1 | MIT | Morphix.Tests | Testhost |
| xunit | 2.9.3 | Apache-2.0 | Morphix.Tests | Testframework |
| xunit.runner.visualstudio | 4.0.1 | Apache-2.0 | Morphix.Tests | Testadapter für `dotnet test` und Visual Studio |
| NSubstitute | 6.2.0 | BSD-3-Clause | Morphix.Tests | Test-Doubles (Fakes) |
| coverlet.collector | 10.1.0 | MIT | Morphix.Tests | Abdeckungsmessung |

`Morphix.Core` bindet **kein** Paket ein.

### 3.2 Wichtige transitive Pakete

| Paket | Version | Lizenz | Kommt von | Bemerkung |
|---|---|---|---|---|
| OxyPlot.Core | 2.2.0 | MIT | OxyPlot.Wpf | liegt in `release/` (`OxyPlot.dll`) |
| OxyPlot.Wpf.Shared | 2.2.0 | MIT | OxyPlot.Wpf | liegt in `release/` |
| Microsoft.Extensions.DependencyInjection.Abstractions | 11.0.0 | MIT | Microsoft.Extensions.DependencyInjection | liegt in `release/` |
| xunit.core, xunit.assert (und weitere) | 2.9.3 | Apache-2.0 | xunit | nur Tests |
| weitere Pakete des Test-SDK (TestPlatform) | – | meist MIT | Microsoft.NET.Test.Sdk | nur Tests, nicht ausgeliefert |

### 3.3 Bordmittel ohne Paket

`System.Text.Json` (Profile, Aktionsprotokoll), `System.IO.Compression` (ZIP-Export), `System.Net.WebUtility` (HTML-Kodierung),
`System.Security.Principal` (Administratorrechte), `System.Diagnostics.Eventing.Reader` (kommt mit dem EventLog-Paket),
WPF-`WebBrowser` (Berichtsvorschau).

### 3.4 Was ausgeliefert wird

`release/` (framework-dependent, win-x64) enthält neben den eigenen Assemblys `CommunityToolkit.Mvvm`,
`Microsoft.Extensions.DependencyInjection(.Abstractions)`, `Microsoft.Win32.TaskScheduler`, `OxyPlot`, `OxyPlot.Wpf`,
`OxyPlot.Wpf.Shared`, `System.Diagnostics.EventLog(.Messages)` und `System.ServiceProcess.ServiceController` sowie die deutschen
Bibliotheksressourcen. Testpakete werden nicht ausgeliefert. Zur Laufzeit wird die .NET 10 Desktop Runtime vorausgesetzt.

---

## 4. Abweichungen vom Architekturdokument

Gegenübergestellt sind die Vorgaben des Architekturdokuments (Kapitelnummern wie im PDF) und der Ist-Zustand. „Abweichung“ heißt:
Der Ist-Zustand widerspricht dem Dokument oder setzt es anders um. „Ergänzung“ heißt: Es gibt etwas, das das Dokument nicht
nennt, ohne ihm zu widersprechen. Die Spalte „Dokumentiert in“ nennt, wo die Entscheidung begründet ist.

### 4.1 Abweichungen

| Nr. | Kapitel | Vorgabe im Architekturdokument | Ist-Zustand | Dokumentiert in |
|---|---|---|---|---|
| A1 | 2.1 Schichten | Präsentation darf nur auf die **Anwendung** zugreifen. | ViewModels verwenden Domänentypen (`Systemprofil`, `Diagnose`, `Dienst` …) und rufen zustandslose Domänenfunktionen **direkt** auf: `LogFilter` und `FehlerGruppierer` in `LogsUndFehlerViewModel`, `FehlerVerlauf` in `DashboardViewModel`, `ZeitraumRegel`-Konstanten in `MainViewModel`. Der Compiler verhindert das nicht, weil Anwendung und Domäne beide in `Morphix.Core` liegen. | nur hier |
| A2 | 2.1 Schichten | Abhängigkeiten zeigen nur nach innen; die Präsentation kennt die Infrastruktur nicht. | `Morphix.App` hat eine **Projektreferenz auf `Morphix.Infrastructure`**. Genutzt wird sie nur in `App.xaml.cs` (Composition Root, `BaueContainer`), nirgends in Views oder ViewModels. | nur hier (vgl. Kap. 2.4: DI-Registrierung in `App.xaml.cs`) |
| A3 | 3 Struktur (`DienstService`) | „prüft GR03 (Profil, Rechte, Bestätigung) **und GR04 (Demo)**“. | GR03 prüft der `DienstService`. GR04 prüft er **nicht**: Die Fabrik liefert im Demo-Modus `DemoDienstGateway` und `DemoRechtePruefer`, die simulieren bzw. „Rechte vorhanden“ melden (kein `if (istDemo)` im Service). Der Service übernimmt `Profil.IstDemo` nur als Datum für das Aktionsprotokoll. Im Demo-Modus entfällt dadurch die Administratorrechte-Prüfung faktisch. | AENDERUNGEN §7, §12 |
| A4 | 3 Struktur / 4 Verhalten (`IBerichtExporter`, Maskierung) | Die Maskierung läuft im `BerichtService` **vor dem Export**; Schnittstelle `IBerichtExporter` exportiert „Bericht und Logs“. | Das HTML wird im `BerichtService` maskiert. Die Logkopien im ZIP maskiert der Exporter **zeilenweise beim Kopieren** über einen vom `BerichtService` übergebenen `zeilenFilter`. Dafür ist `IBerichtExporter.ExportierenAsync` um den Parameter `zeilenFilter` erweitert, und es gibt die zusätzliche Methode `KannSchreiben` (Prüfung „Exportordner beschreibbar“). | AENDERUNGEN §8 |
| A5 | 2.1 / 3 (`HtmlZipExporter`, „HTML/ZIP“-Adapter) | Die Infrastruktur enthält Adapter für „HTML/ZIP“. | Die HTML-Erzeugung liegt in `Morphix.Core` (`HtmlBerichtErzeuger`, intern in der Anwendungsschicht, aufgerufen vom `BerichtService`). `HtmlZipExporter` schreibt nur die ZIP-Datei; der Name ist irreführend, er erzeugt kein HTML. | nur hier |
| A6 | 5 Nebenläufigkeit | „Einlesen und Gruppieren per `Task.Run`.“ | Gruppieren läuft per `Task.Run` (`DiagnoseService`). Das **Einlesen** der Dateien ist asynchrones Dateilesen (`FileStream` mit `useAsync`) ohne `Task.Run`; `Task.Run` nutzen die Windows-Adapter für Dienste, Aufgaben und Ereignisse (synchrone Windows-APIs). Das **Filtern und Neugruppieren** in „Logs und Fehler“ läuft synchron im UI-Thread (gemessen: bei 1 Mio. Einträgen rund 20–45 ms). | nur hier (Messung: `testprotokoll/M8_Qualitaet.md`) |
| A7 | 2.2 Asynchrone Verarbeitung („`IProgress`“) | Langläufer melden Fortschritt über `IProgress`. | Nur die **Analyse** meldet Fortschritt. Der **ZIP-Export** läuft asynchron, aber ohne `IProgress` und ohne Abbruchmöglichkeit in der Oberfläche; ebenso die Dienstaktionen (Wartezeit bis 30 s). | TECHNICAL_DEBT Nr. 6, 15 |
| A8 | 1.2 Bibliotheken | Tabelle nennt: CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection, System.ServiceProcess.ServiceController, System.Diagnostics.EventLog, TaskScheduler, OxyPlot.Wpf, xUnit, NSubstitute, coverlet.collector. | Zusätzlich eingebunden: **Microsoft.NET.Test.Sdk** und **xunit.runner.visualstudio** (Test-Infrastruktur, in `CLAUDE.md` erlaubt, im Dokument nicht aufgeführt). Die Microsoft-Pakete haben Version 11.0.0, also eine höhere Hauptversion als das Zielframework .NET 10. Transitiv ausgeliefert werden außerdem `OxyPlot.Core`, `OxyPlot.Wpf.Shared` und `Microsoft.Extensions.DependencyInjection.Abstractions`. | Abschnitt 3 dieses Dokuments |
| A9 | 3 Struktur (Klassenliste, `IDatenquellenFabrik`) | Die Fabrik liefert „passend zum Profil (`istDemo`) die Datenquellen“. | Die Fabrik ist **zustandsbehaftet**: Für Demo-Profile merkt sie sich das simulierte Dienstsystem je Profil (solange sich die Dienstliste nicht ändert), damit Dienste-Ansicht und Analyse denselben Zustand sehen. Sie liefert ein `Datenquellen`-Paket, das auch den `IRechtePruefer` enthält. | AENDERUNGEN §7 |
| A10 | 3 Struktur (`MainViewModel`, Datenfluss) | `MainViewModel`: „Profil und Zeitraum wählen, Analyse starten, Navigation“. | Zusätzlich verteilt das `MainViewModel` das Analyseergebnis aktiv an die Ansichten (`Zeige(diagnose)` an Dashboard, Logs, Aufgaben, Bericht) und setzt sie beim Profilwechsel zurück. Es gibt keinen gemeinsamen Diagnose-Zustand (Service oder Store); die ViewModels sind Singletons. | nur hier |
| A11 | 5 Querschnitt: Speicherorte | Profile unter `%AppData%\Morphix\Profile`; Demo-Daten im Programmordner. | Weitere Speicherorte: `%AppData%\Morphix\einstellungen.json` (zuletzt genutztes Profil), `%AppData%\Morphix\aktionen.jsonl` (Aktionsprotokoll, US16) und `%TEMP%\Morphix\Morphix_Bericht_*.html` (Bericht im Browser, wird nach einer Stunde beim nächsten Öffnen gelöscht). | AENDERUNGEN §9, §12 |
| A12 | 5 Querschnitt: Fehlerbehandlung | „Leser fangen Ein-/Ausgabefehler je Datei ab, überspringen die Datei und sammeln eine Meldung.“ | Gilt für den `DateiLogLeser`. Über den Wortlaut hinaus: `DiagnoseService` fängt Fehler je **Ereignisprotokoll** ab, `WindowsAufgabenGateway` je **Aufgabe**, der Exporter vermerkt nicht kopierbare Logs in `logs/_nicht_kopiert.txt`. Das `WindowsDienstGateway` meldet nicht lesbare Dienste als Status „Unbekannt“. | AENDERUNGEN §8, §9 |
| A13 | 2.1 Abbildungen, 3 und 4 | Das Dokument verweist auf `schichten.png`, `klassen.png`, `sequenz.png`. | Diese drei Dateien liegen **nicht** im Repository (`docs/` enthält `UML_Diagramm.png` und `mockup-hauptfenster.png`). Dieses Dokument liefert die Ist-Diagramme als Mermaid. | – |

### 4.2 Ergänzungen (im Architekturdokument nicht genannt)

| Nr. | Bereich | Ergänzung |
|---|---|---|
| E1 | Domäne | `ProfilValidator` (GR02 und Feldregeln), `LogFilter` (US04), `FehlerVerlauf` (US12), `AnzeigeTexte`, `Systeminfo`, `LogleseErgebnis`, `Validierungsergebnis`. Der Datensatz `Aufgabenlauf` aus dem Datenmodell ist angelegt, wird aber nicht verwendet. |
| E2 | Schnittstellen | `IEreignisLeser` (Ereignisanzeige als eigener Port neben `ILogLeser`) und `IAktionsProtokoll` (US16). |
| E3 | Infrastruktur | Zugriffsschnittstellen `IDienstZugriff`, `IAufgabenZugriff`, `IEreignisZugriff` als zusätzliche Nahtstelle in den Adaptern (siehe 1.4); `JsonLinesAktionsProtokoll`. |
| E4 | Präsentation | `LogsUndFehlerViewModel`, `AufgabenViewModel`, `ProfilBearbeitenViewModel`, `VerlaufDiagramm`, `WebBrowserHtml`; Dialogabstraktionen `IBestaetigungsDialog`, `IExportDialog`, `IProfilDialogService`, `IBrowserOeffner`, damit ViewModels ohne Fenster testbar sind. |
| E5 | Verhalten | `BerichtViewModel` bietet zusätzlich „Im Browser öffnen“. Die Maskierung lässt sich nur nach Bestätigung ausschalten und ist nach jeder Analyse und jedem Profilwechsel wieder an (GR10 „bewusst pro Bericht“). Der `DienstService` protokolliert jede Aktion, auch abgelehnte (US16). |
| E6 | Datenmodell | `Dienstaktion` hat zusätzlich `Dienstname`, `Profil`, `Meldung`, `Simuliert` (Spezifikation: vier Attribute). |

### 4.3 Bestätigt (keine Abweichung)

Zur Abgrenzung: Folgende Aussagen des Architekturdokuments gelten unverändert. Vier Schichten und Projekte wie beschrieben,
`Morphix.Core` zielt auf `net10.0` ohne Windows-Bezug und referenziert nichts; Infrastruktur implementiert nur Ports aus dem Core;
Demo-Modus ist ein Austausch der Adapter über die Fabrik; Rechteprüfung (GR03) im Service; parallele Erfassung per
`Task.WhenAll`; Statusabfrage der Dienste per `PeriodicTimer` alle 5 s; lesender Dateizugriff mit `FileAccess.Read` und
`FileShare.ReadWrite`; HTML-Kodierung aller Log-Inhalte; Zeilenmuster je Logquelle (Kap. 6: Attribut `zeilenmuster`); .NET 10
(Kap. 6); alle Abhängigkeiten werden in `App.xaml.cs` registriert.

### 4.4 Abweichungen von Spezifikation und Umsetzungsplan (zur Vollständigkeit)

Diese betreffen nicht das Architekturdokument und stehen ausführlich in `docs/AENDERUNGEN.md`: Meldungsmuster nur aus der ersten
Zeile (§1), selbst formulierte Validierungsmeldungen (§2), IT-betriebliche Demo-Daten statt `ZE-`-Namen (§3), Zeitraum auf
Tagesbasis (§4), Dateiauswahl nach „Änderungsdatum nicht vor Beginn“ (§5), Bedienung der Dienststeuerung (§7), Begrenzungen im
Bericht (§8), NFA11 nach Anforderung v1.1 (§10), `release/` im Repository (§10).
