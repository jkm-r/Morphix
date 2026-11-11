using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Morphix.Core.Domaene;

namespace Morphix.App.ViewModels;

/// <summary>Eintrag der Schweregrad-Auswahl; <see cref="Wert"/> ist <c>null</c> für „Alle“.</summary>
/// <param name="Titel">Anzeigetext.</param>
/// <param name="Wert">Der Schweregrad oder <c>null</c> für alle.</param>
public sealed record SchweregradAuswahl(string Titel, Schweregrad? Wert);

/// <summary>
/// ViewModel der Ansicht „Logs und Fehler“: Filter (Schweregrad, Suchbegriff, Zeitraum) wirken sofort auf die
/// Fehlergruppen und die Einträge (US04); ein Doppelklick auf eine Gruppe zeigt deren Einträge (US05).
/// </summary>
public sealed partial class LogsUndFehlerViewModel : ObservableViewModel
{
    /// <summary>Höchstzahl gleichzeitig angezeigter Einträge, damit die Tabelle auch bei großen Logs flüssig bleibt.</summary>
    public const int MaxAngezeigteEintraege = 10_000;

    private Diagnose? _diagnose;
    private (string Quelle, string Muster)? _gruppenSchluessel;

    /// <summary>Auswahlmöglichkeiten des Schweregrad-Filters.</summary>
    public IReadOnlyList<SchweregradAuswahl> Schweregrade { get; } =
    [
        new("Alle Schweregrade", null),
        new("Info", Schweregrad.Info),
        new("Warnung", Schweregrad.Warnung),
        new("Fehler", Schweregrad.Fehler),
        new("Kritisch", Schweregrad.Kritisch),
    ];

    /// <summary>Der gewählte Schweregrad-Filter.</summary>
    [ObservableProperty]
    public partial SchweregradAuswahl AusgewaehlterSchweregrad { get; set; }

    /// <summary>Suchbegriff (Meldung oder Quelle), höchstens 200 Zeichen.</summary>
    [ObservableProperty]
    public partial string Suchbegriff { get; set; } = "";

    /// <summary>Fehlertext zum Suchbegriff (leer, wenn gültig).</summary>
    [ObservableProperty]
    public partial string SuchbegriffFehler { get; set; } = "";

    /// <summary>Filter: erster Tag.</summary>
    [ObservableProperty]
    public partial DateTime? VonTag { get; set; }

    /// <summary>Filter: letzter Tag.</summary>
    [ObservableProperty]
    public partial DateTime? BisTag { get; set; }

    /// <summary>Fehlergruppen der gefilterten Einträge (nur Fehler und Kritisch, GR07).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Fehlergruppe> Gruppen { get; set; } = [];

    /// <summary>Angezeigte Einträge: die der gewählten Gruppe oder alle gefilterten.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<Logeintrag> Eintraege { get; set; } = [];

    /// <summary>Die per Doppelklick gewählte Gruppe oder <c>null</c>, wenn alle Einträge gezeigt werden.</summary>
    [ObservableProperty]
    public partial Fehlergruppe? GeoeffneteGruppe { get; set; }

    /// <summary>Überschrift über der Eintragsliste.</summary>
    [ObservableProperty]
    public partial string EintraegeTitel { get; set; } = "Einträge";

    /// <summary>Zusammenfassung (Anzahl Einträge und Gruppen) bzw. Hinweis ohne Diagnose.</summary>
    [ObservableProperty]
    public partial string Zusammenfassung { get; set; } = "Noch keine Analyse durchgeführt.";

    /// <summary>Erzeugt das ViewModel.</summary>
    public LogsUndFehlerViewModel()
    {
        AusgewaehlterSchweregrad = Schweregrade[0];
    }

    /// <summary>Zeigt die Einträge einer Diagnose an; <c>null</c> setzt die Ansicht zurück. Die Filter bleiben bestehen.</summary>
    public void Zeige(Diagnose? diagnose)
    {
        _diagnose = diagnose;
        _gruppenSchluessel = null;
        Aktualisiere();
    }

    partial void OnAusgewaehlterSchweregradChanged(SchweregradAuswahl value) => Aktualisiere();

    partial void OnSuchbegriffChanged(string value)
    {
        SuchbegriffFehler = LogFilter.PruefeSuchbegriff(value).MeldungenFuer("Suchbegriff").FirstOrDefault() ?? "";
        Aktualisiere();
    }

    partial void OnVonTagChanged(DateTime? value) => Aktualisiere();

    partial void OnBisTagChanged(DateTime? value) => Aktualisiere();

    [RelayCommand]
    private void GruppeOeffnen(Fehlergruppe? gruppe)
    {
        if (gruppe is null)
        {
            return;
        }

        _gruppenSchluessel = (gruppe.Quelle, gruppe.Meldungsmuster);
        Aktualisiere();
    }

    [RelayCommand]
    private void AlleEintraegeAnzeigen()
    {
        _gruppenSchluessel = null;
        Aktualisiere();
    }

    private void Aktualisiere()
    {
        if (_diagnose is null)
        {
            Gruppen = [];
            Eintraege = [];
            GeoeffneteGruppe = null;
            EintraegeTitel = "Einträge";
            Zusammenfassung = "Noch keine Analyse durchgeführt.";
            return;
        }

        var gefiltert = LogFilter.Anwenden(
            _diagnose.Logeintraege, AusgewaehlterSchweregrad.Wert, Suchbegriff, VonTag, BisTag).ToList();
        Gruppen = FehlerGruppierer.Gruppiere(gefiltert);

        GeoeffneteGruppe = _gruppenSchluessel is { } s
            ? Gruppen.FirstOrDefault(g => g.Quelle == s.Quelle && g.Meldungsmuster == s.Muster)
            : null;

        var quelle = GeoeffneteGruppe is null ? gefiltert : (IReadOnlyList<Logeintrag>)GeoeffneteGruppe.Eintraege;
        Eintraege = quelle.Count > MaxAngezeigteEintraege ? [.. quelle.Take(MaxAngezeigteEintraege)] : quelle;

        EintraegeTitel = GeoeffneteGruppe is null
            ? "Einträge"
            : $"Einträge der Gruppe „{GeoeffneteGruppe.Meldungsmuster}“";
        Zusammenfassung = $"{gefiltert.Count:N0} Einträge, {Gruppen.Count:N0} Fehlergruppen"
            + (quelle.Count > MaxAngezeigteEintraege
                ? $" – angezeigt werden die ersten {MaxAngezeigteEintraege:N0} von {quelle.Count:N0} Einträgen."
                : ".");
    }
}
