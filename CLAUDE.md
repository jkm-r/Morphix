# Morphix – Projektkontext für Claude Code

Morphix ist eine Windows-Desktopanwendung für Administratoren und Support-Mitarbeitende von
Zeiterfassungs- und Zutrittskontrollsystemen. Sie sammelt Logdateien, Windows-Ereignisse,
Dienststatus und Ergebnisse der Aufgabenplanung an einem Ort, gruppiert Fehler und erzeugt einen
standardisierten Support-Bericht (HTML, als ZIP mit den Logs exportierbar).

Das Projekt ist ein Studienportfolio (IU, Kurs „Projekt: Software Engineering“). Die verbindlichen
Vorgaben stehen in `docs/` (Anforderungs-, Spezifikations- und Architekturdokument). **Bei
Widersprüchen gelten die Dokumente in `docs/`, nicht diese Datei.** Weiche nie stillschweigend davon
ab: Wenn etwas nicht umsetzbar oder unklar ist, frag nach oder notiere es in `docs/TECHNICAL_DEBT.md`.

## Arbeitsweise

- Arbeite meilensteinweise nach `docs/UMSETZUNGSPLAN.md`. Nach jedem Meilenstein: bauen, alle Tests
  ausführen, kurz zusammenfassen, was entstanden ist, und **anhalten**, bis ich weitermache.
- Erkläre bei nicht offensichtlichen Entscheidungen in 1–2 Sätzen, warum – ich muss den Code in der
  Reflexion selbst erklären können.
- Kleine, thematisch saubere Commits mit deutscher Commit-Nachricht (z. B. „FehlerGruppierer mit
  Normalisierung und Tests“). Nicht pushen, außer ich sage es.
- Keine zusätzlichen NuGet-Pakete außer den unten erlaubten, ohne vorher zu fragen.
- Keinen Code aus fremden Anwendungen kopieren (Prüfungsvorgabe). Alles selbst schreiben.

## Technik

- C# 14, **.NET 10**, WPF. Visual Studio 2026.
- Erlaubte Pakete:
  - CommunityToolkit.Mvvm (ObservableObject, [ObservableProperty], [RelayCommand])
  - Microsoft.Extensions.DependencyInjection
  - System.ServiceProcess.ServiceController
  - System.Diagnostics.EventLog
  - TaskScheduler (Namespace Microsoft.Win32.TaskScheduler)
  - OxyPlot.Wpf (nur für Could-Anforderung US12)
  - Tests: xunit, xunit.runner.visualstudio, Microsoft.NET.Test.Sdk, NSubstitute, coverlet.collector
- Bordmittel: System.Text.Json, System.IO.Compression, System.Net.WebUtility, System.Security.Principal.

## Solution-Struktur

```
Morphix.sln
src/
  Morphix.Core/            net10.0          (KEIN Windows-/WPF-Bezug!)
    Domaene/               Geschäftsobjekte, Enums, Regeln, LogParser, FehlerGruppierer, DatenMaskierer
    Schnittstellen/        ILogLeser, IDienstGateway, IAufgabenGateway, IEreignisLeser,
                           IDatenquellenFabrik, IProfilRepository, IBerichtExporter, IRechtePruefer
    Anwendung/             DiagnoseService, DienstService, BerichtService, ProfilService
  Morphix.Infrastructure/  net10.0-windows  Windows-Adapter + Demo-Adapter
  Morphix.App/             net10.0-windows, UseWPF   Views, ViewModels, App.xaml.cs (DI)
tests/
  Morphix.Tests/           net10.0-windows
demo/                      Demo-Profil und Demo-Logdateien (werden ins Ausgabeverzeichnis kopiert)
docs/                      Projektdokumente
```

## Architekturregeln (Architekturdokument, Kap. 2)

- Schichten: Präsentation → Anwendung → Domäne. Infrastruktur implementiert nur Schnittstellen aus
  Morphix.Core. Morphix.Core referenziert kein anderes Projekt.
- MVVM: keine Logik im Code-Behind (außer reinem View-Verhalten). ViewModels kennen nur Services.
- Demo-Modus ist **kein `if (istDemo)` im Code**, sondern ein Austausch der Adapter über
  `IDatenquellenFabrik.Erzeugen(profil)` (liefert echte oder Demo-Implementierungen).
- Alles, was länger als ~100 ms dauern kann, ist async; Fortschritt über `IProgress<int>`.
- Die Rechteprüfung (GR03) passiert im `DienstService`, nicht nur in der Oberfläche.

## Sprache und Konventionen

- Fachbegriffe deutsch wie im Datenmodell (Systemprofil, Logquelle, Dienst, GeplanteAufgabe,
  Aufgabenlauf, Diagnose, Logeintrag, Fehlergruppe, SupportBericht, Dienstaktion); Umlaute in
  Bezeichnern vermeiden (Pruefen, Naechster). Framework-Begriffe englisch lassen (ViewModel, Command).
- Oberfläche vollständig deutsch (NFA04). XML-Doku-Kommentare (`///`) auf Deutsch für alle
  öffentlichen Typen und Methoden.
- `Nullable` und `TreatWarningsAsErrors` aktiviert. Dateibezogene Namespaces.

## Fachliche Kernregeln (Spezifikation, Kap. 3)

- GR01 Zeitraum: von < bis, bis nicht in der Zukunft, höchstens 30 Tage.
- GR02 Profil: eindeutiger Name (3–50 Zeichen), ≥ 1 Logquelle, ≥ 1 Dienst.
- GR03 Dienststeuerung nur für Profildienste, mit Adminrechten, nach Bestätigung.
- GR04 Im Demo-Modus werden Dienstaktionen nur simuliert.
- GR05 Logdateien nur lesend öffnen (`FileAccess.Read`, `FileShare.ReadWrite`), nie ändern/verschieben/löschen.
- GR06 Zeile ohne Zeitstempel gehört zum vorherigen Logeintrag.
- GR07 Fehlergruppen nur aus Schweregrad Fehler oder Kritisch.
- GR08 Aufgabe fehlgeschlagen, wenn letzter Ergebniscode ≠ 0.
- GR09 Bericht nur aus abgeschlossener Diagnose.
- GR10 Maskierung standardmäßig an, pro Bericht bewusst abschaltbar.

## Sicherheit

- Kein Netzwerkzugriff (NFA06). Keine HttpClient-, Socket- oder Telemetrie-Aufrufe.
- Alle Log-Inhalte vor dem Einfügen in den HTML-Bericht mit `WebUtility.HtmlEncode` kodieren.
- Keine echten Kunden- oder Personendaten, auch nicht in Tests oder Demo-Daten.

## Befehle

- Bauen: `dotnet build`
- Tests: `dotnet test`
- Abdeckung: `dotnet test --collect:"XPlat Code Coverage"`
- Starten: `dotnet run --project src/Morphix.App`
