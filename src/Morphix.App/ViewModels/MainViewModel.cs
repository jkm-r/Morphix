using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Morphix.App.Dialoge;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;

namespace Morphix.App.ViewModels;

/// <summary>
/// ViewModel des Hauptfensters: Navigation, Kopfleiste (Profil, Zeitraum, Analyse) und Statusleiste.
/// </summary>
public sealed partial class MainViewModel : ObservableViewModel
{
    private readonly ProfilService _profilService;
    private readonly IProfilDialogService _dialoge;
    private readonly DiagnoseService _diagnoseService;
    private readonly DashboardViewModel _dashboard;
    private readonly LogsUndFehlerViewModel _logs;
    private bool _aktualisiereProfile;

    /// <summary>Die Einträge der linken Navigation in Anzeigereihenfolge.</summary>
    public IReadOnlyList<NavigationsEintrag> Navigation { get; }

    /// <summary>Alle Profile für die Auswahl in der Kopfleiste.</summary>
    public ObservableCollection<Systemprofil> Profile { get; } = [];

    /// <summary>Der gerade ausgewählte Navigationseintrag.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AktuelleAnsicht))]
    public partial NavigationsEintrag AusgewaehlterEintrag { get; set; }

    /// <summary>
    /// ViewModel der aktuell angezeigten Ansicht. Die DataTemplates in <c>App.xaml</c> ordnen
    /// jedem ViewModel-Typ das passende View zu.
    /// </summary>
    public ObservableViewModel AktuelleAnsicht => AusgewaehlterEintrag.Ansicht;

    /// <summary>Das gewählte Profil.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProfilBearbeitenCommand), nameof(AnalyseStartenCommand))]
    public partial Systemprofil? AusgewaehltesProfil { get; set; }

    /// <summary>Erster Tag des Analysezeitraums (Standard: vor 6 Tagen, also die letzten 7 Tage inklusive heute).</summary>
    [ObservableProperty]
    public partial DateTime? ZeitraumVon { get; set; } = DateTime.Today.AddDays(-6);

    /// <summary>Letzter Tag des Analysezeitraums (Standard: heute).</summary>
    [ObservableProperty]
    public partial DateTime? ZeitraumBis { get; set; } = DateTime.Today;

    /// <summary>Fehlertext zum Zeitraum (GR01); leer, wenn der Zeitraum gültig ist.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyseStartenCommand))]
    public partial string ZeitraumFehler { get; set; } = "";

    /// <summary>Zeigt den Hinweis „Demo-Modus“ in der Kopfleiste an, wenn das Profil ein Demo-Profil ist.</summary>
    public bool IstDemoModus => AusgewaehltesProfil?.IstDemo ?? false;

    /// <summary>Text der Statusleiste.</summary>
    [ObservableProperty]
    public partial string Statusmeldung { get; set; } = "Bereit.";

    /// <summary>Fortschritt der laufenden Analyse in Prozent.</summary>
    [ObservableProperty]
    public partial int Fortschritt { get; set; }

    /// <summary>Ob gerade eine Analyse läuft.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AnalyseStartenCommand))]
    public partial bool IstAnalyseLaeuft { get; set; }

    /// <summary>
    /// Erzeugt das Hauptfenster-ViewModel. Die Abhängigkeiten kommen per Dependency Injection.
    /// </summary>
    public MainViewModel(
        ProfilService profilService,
        DiagnoseService diagnoseService,
        IProfilDialogService dialoge,
        DashboardViewModel dashboard,
        LogsUndFehlerViewModel logsUndFehler,
        DiensteViewModel dienste,
        AufgabenViewModel aufgaben,
        BerichtViewModel bericht)
    {
        _profilService = profilService;
        _diagnoseService = diagnoseService;
        _dialoge = dialoge;
        _dashboard = dashboard;
        _logs = logsUndFehler;
        Navigation =
        [
            new("Dashboard", dashboard),
            new("Logs und Fehler", logsUndFehler),
            new("Dienste", dienste),
            new("Aufgaben", aufgaben),
            new("Bericht", bericht),
        ];
        AusgewaehlterEintrag = Navigation[0];

        // Der Button „Bericht erstellen“ im Dashboard führt zur Ansicht „Bericht“.
        dashboard.BerichtAngefordert += () => AusgewaehlterEintrag = Navigation[^1];
        PruefeZeitraum();
    }

    /// <summary>
    /// Lädt beim Start die Profile (P1). Beim ersten Start wird das Demo-Profil angelegt und ausgewählt,
    /// sonst das zuletzt genutzte Profil.
    /// </summary>
    public async Task InitialisierenAsync()
    {
        try
        {
            var start = await _profilService.StartenAsync();
            AktualisiereProfile(start.Aktiv);
            Statusmeldung = start.DemoProfilAngelegt
                ? "Erster Start: Das Demo-Profil wurde angelegt. Wähle einen Zeitraum und starte die Analyse."
                : "Bereit.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Statusmeldung = $"Die Profile konnten nicht geladen werden: {ex.Message}";
        }
    }

    partial void OnAusgewaehltesProfilChanged(Systemprofil? value)
    {
        OnPropertyChanged(nameof(IstDemoModus));

        // Die Ergebnisse gehören zum bisherigen Profil und werden nicht stehen gelassen.
        _dashboard.Zeige(null);
        _logs.Zeige(null);

        if (value is not null && !_aktualisiereProfile)
        {
            _ = MerkeProfilAsync(value);
        }
    }

    partial void OnZeitraumVonChanged(DateTime? value) => PruefeZeitraum();

    partial void OnZeitraumBisChanged(DateTime? value) => PruefeZeitraum();

    /// <summary>
    /// Startet die Analyse (P7, Schritte P2/P3/P6) für das gewählte Profil und den Zeitraum.
    /// Der Zeitraum gilt tageweise: „von“ ab 00:00 Uhr, „bis“ bis 23:59:59, höchstens bis jetzt.
    /// </summary>
    [RelayCommand(CanExecute = nameof(KannAnalysieren))]
    private async Task AnalyseStartenAsync()
    {
        var profil = AusgewaehltesProfil!;
        var (von, bis, _) = _diagnoseService.PruefeTage(ZeitraumVon!.Value, ZeitraumBis!.Value);

        IstAnalyseLaeuft = true;
        Fortschritt = 0;
        Statusmeldung = $"Analyse von „{profil.Name}“ läuft …";
        try
        {
            var diagnose = await _diagnoseService.ErstellenAsync(
                profil, von, bis, new Progress<int>(p => Fortschritt = p));

            _dashboard.Zeige(diagnose);
            _logs.Zeige(diagnose);
            Statusmeldung = BaueErgebnismeldung(diagnose);
        }
        catch (Exception ex)
        {
            // Eine fehlgeschlagene Analyse darf die Anwendung nicht beenden (NFA07).
            Statusmeldung = $"Die Analyse ist fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IstAnalyseLaeuft = false;
        }
    }

    private bool KannAnalysieren() =>
        !IstAnalyseLaeuft && AusgewaehltesProfil is not null && ZeitraumFehler.Length == 0;

    private void PruefeZeitraum()
    {
        if (ZeitraumVon is not { } von || ZeitraumBis is not { } bis)
        {
            ZeitraumFehler = ZeitraumRegel.Meldung;
            return;
        }

        var (_, _, ergebnis) = _diagnoseService.PruefeTage(von, bis);
        ZeitraumFehler = ergebnis.MeldungenFuer(ZeitraumRegel.Feld).FirstOrDefault() ?? "";
    }

    private static string BaueErgebnismeldung(Diagnose d)
    {
        var meldung = $"Analyse abgeschlossen · {d.GeleseneDateien.Count} Dateien ({FormatiereGroesse(d.GeleseneBytes)}) eingelesen";
        return d.UebersprungeneDateien.Count > 0
            ? $"{meldung} · {d.UebersprungeneDateien.Count} übersprungen"
            : meldung;
    }

    private static string FormatiereGroesse(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} Byte",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB",
    };

    [RelayCommand]
    private void ProfilNeu() => BearbeiteProfil(null);

    [RelayCommand(CanExecute = nameof(KannBearbeiten))]
    private void ProfilBearbeiten() => BearbeiteProfil(AusgewaehltesProfil);

    private bool KannBearbeiten() => AusgewaehltesProfil is not null;

    private void BearbeiteProfil(Systemprofil? profil)
    {
        var gespeichert = _dialoge.Bearbeiten(profil);
        if (gespeichert is null)
        {
            return;
        }

        AktualisiereProfile(gespeichert);
        Statusmeldung = $"Profil „{gespeichert.Name}“ gespeichert.";
    }

    /// <summary>Füllt die Auswahlliste neu und wählt das angegebene Profil aus.</summary>
    private void AktualisiereProfile(Systemprofil? auswahl)
    {
        _aktualisiereProfile = true;
        try
        {
            Profile.Clear();
            foreach (var p in _profilService.Profile)
            {
                Profile.Add(p);
            }

            AusgewaehltesProfil = auswahl;
        }
        finally
        {
            _aktualisiereProfile = false;
        }

        if (auswahl is not null)
        {
            _ = MerkeProfilAsync(auswahl);
        }
    }

    private async Task MerkeProfilAsync(Systemprofil profil)
    {
        try
        {
            await _profilService.MerkeAktivAsync(profil);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Statusmeldung = $"Das zuletzt genutzte Profil konnte nicht gemerkt werden: {ex.Message}";
        }
    }
}
