using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Morphix.App.ViewModels;

/// <summary>
/// ViewModel des Hauptfensters: Navigation, Kopfleiste (Profil, Zeitraum, Analyse) und Statusleiste.
/// </summary>
public sealed partial class MainViewModel : ObservableViewModel
{
    /// <summary>Die Einträge der linken Navigation in Anzeigereihenfolge.</summary>
    public IReadOnlyList<NavigationsEintrag> Navigation { get; }

    /// <summary>Profilnamen für die Auswahl in der Kopfleiste (wird in M3 aus dem Profil-Repository gefüllt).</summary>
    public ObservableCollection<string> Profile { get; } = [];

    /// <summary>Der gerade ausgewählte Navigationseintrag.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AktuelleAnsicht))]
    public partial NavigationsEintrag AusgewaehlterEintrag { get; set; }

    /// <summary>
    /// ViewModel der aktuell angezeigten Ansicht. Die DataTemplates in <c>App.xaml</c> ordnen
    /// jedem ViewModel-Typ das passende View zu.
    /// </summary>
    public ObservableViewModel AktuelleAnsicht => AusgewaehlterEintrag.Ansicht;

    /// <summary>Name des gewählten Profils.</summary>
    [ObservableProperty]
    public partial string? AusgewaehltesProfil { get; set; }

    /// <summary>Beginn des Analysezeitraums.</summary>
    [ObservableProperty]
    public partial DateTime? ZeitraumVon { get; set; }

    /// <summary>Ende des Analysezeitraums.</summary>
    [ObservableProperty]
    public partial DateTime? ZeitraumBis { get; set; }

    /// <summary>Zeigt den Hinweis „Demo-Modus“ in der Kopfleiste an, wenn das Profil ein Demo-Profil ist.</summary>
    [ObservableProperty]
    public partial bool IstDemoModus { get; set; }

    /// <summary>Text der Statusleiste.</summary>
    [ObservableProperty]
    public partial string Statusmeldung { get; set; } = "Bereit.";

    /// <summary>
    /// Erzeugt das Hauptfenster-ViewModel. Die Ansichts-ViewModels kommen per Dependency Injection.
    /// </summary>
    public MainViewModel(
        DashboardViewModel dashboard,
        LogsUndFehlerViewModel logsUndFehler,
        DiensteViewModel dienste,
        AufgabenViewModel aufgaben,
        BerichtViewModel bericht)
    {
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

    /// <summary>Startet die Analyse. In M1 noch ohne Funktion; die Analyse folgt in M4.</summary>
    [RelayCommand]
    private void AnalyseStarten()
    {
        Statusmeldung = "Die Analyse ist noch nicht implementiert (folgt in Meilenstein M4).";
    }
}
