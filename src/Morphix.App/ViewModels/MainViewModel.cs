using System.IO;
using System.Collections.ObjectModel;
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
    [NotifyCanExecuteChangedFor(nameof(ProfilBearbeitenCommand))]
    public partial Systemprofil? AusgewaehltesProfil { get; set; }

    /// <summary>Beginn des Analysezeitraums.</summary>
    [ObservableProperty]
    public partial DateTime? ZeitraumVon { get; set; } = DateTime.Today.AddDays(-7);

    /// <summary>Ende des Analysezeitraums.</summary>
    [ObservableProperty]
    public partial DateTime? ZeitraumBis { get; set; } = DateTime.Today;

    /// <summary>Zeigt den Hinweis „Demo-Modus“ in der Kopfleiste an, wenn das Profil ein Demo-Profil ist.</summary>
    public bool IstDemoModus => AusgewaehltesProfil?.IstDemo ?? false;

    /// <summary>Text der Statusleiste.</summary>
    [ObservableProperty]
    public partial string Statusmeldung { get; set; } = "Bereit.";

    /// <summary>
    /// Erzeugt das Hauptfenster-ViewModel. Die Abhängigkeiten kommen per Dependency Injection.
    /// </summary>
    public MainViewModel(
        ProfilService profilService,
        IProfilDialogService dialoge,
        DashboardViewModel dashboard,
        LogsUndFehlerViewModel logsUndFehler,
        DiensteViewModel dienste,
        AufgabenViewModel aufgaben,
        BerichtViewModel bericht)
    {
        _profilService = profilService;
        _dialoge = dialoge;
        Navigation =
        [
            new("Dashboard", dashboard),
            new("Logs und Fehler", logsUndFehler),
            new("Dienste", dienste),
            new("Aufgaben", aufgaben),
            new("Bericht", bericht),
        ];
        AusgewaehlterEintrag = Navigation[0];
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
        if (value is not null && !_aktualisiereProfile)
        {
            _ = MerkeProfilAsync(value);
        }
    }

    /// <summary>Startet die Analyse. In M3 noch ohne Funktion; die Analyse folgt in M4.</summary>
    [RelayCommand]
    private void AnalyseStarten()
    {
        Statusmeldung = "Die Analyse ist noch nicht implementiert (folgt in Meilenstein M4).";
    }

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
