using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;

namespace Morphix.App.ViewModels;

/// <summary>
/// ViewModel des Dialogs „Profil bearbeiten“. Jede Änderung wird sofort validiert; Fehler erscheinen am
/// Feld, „Speichern“ bleibt bis zur Korrektur deaktiviert.
/// </summary>
public sealed partial class ProfilBearbeitenViewModel : ObservableViewModel
{
    private readonly ProfilService _service;
    private readonly Systemprofil _vorlage;
    private readonly string? _bisherigerName;

    /// <summary>Wird ausgelöst, wenn der Dialog geschlossen werden soll (<c>true</c> = gespeichert).</summary>
    public event Action<bool>? SchliessenAngefordert;

    /// <summary>Erzeugt das ViewModel.</summary>
    /// <param name="service">Service für Prüfung und Speichern.</param>
    /// <param name="profil">Das zu bearbeitende Profil; <c>null</c> für ein neues Profil.</param>
    public ProfilBearbeitenViewModel(ProfilService service, Systemprofil? profil)
    {
        _service = service;
        _vorlage = profil ?? new Systemprofil();
        _bisherigerName = profil?.Name;

        Name = _vorlage.Name;
        Beschreibung = _vorlage.Beschreibung;
        foreach (var q in _vorlage.Logquellen) { FuegeHinzu(new LogquelleZeile(q)); }
        foreach (var d in _vorlage.Dienste) { FuegeHinzu(new DienstZeile(d)); }
        foreach (var a in _vorlage.Aufgaben) { FuegeHinzu(new AufgabeZeile(a)); }

        if (profil is null)
        {
            // Für ein neues Profil gleich je eine leere Zeile anbieten.
            FuegeHinzu(new LogquelleZeile(new Logquelle()));
            FuegeHinzu(new DienstZeile(new Dienst()));
        }

        Validiere();
    }

    /// <summary>Fensterüberschrift.</summary>
    public string Titel => _bisherigerName is null ? "Neues Profil" : "Profil bearbeiten";

    /// <summary><c>true</c> bei einem Demo-Profil (Hinweis im Dialog).</summary>
    public bool IstDemo => _vorlage.IstDemo;

    /// <summary>Logquellen des Profils.</summary>
    public ObservableCollection<LogquelleZeile> Logquellen { get; } = [];

    /// <summary>Dienste des Profils.</summary>
    public ObservableCollection<DienstZeile> Dienste { get; } = [];

    /// <summary>Geplante Aufgaben des Profils.</summary>
    public ObservableCollection<AufgabeZeile> Aufgaben { get; } = [];

    /// <summary>Das gespeicherte Profil, nachdem der Dialog mit „Speichern“ beendet wurde.</summary>
    public Systemprofil? Gespeichert { get; private set; }

    /// <summary>Profilname.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>Beschreibung.</summary>
    [ObservableProperty]
    public partial string Beschreibung { get; set; }

    /// <summary>Fehlertext zum Namen.</summary>
    [ObservableProperty]
    public partial string NameFehler { get; set; } = "";

    /// <summary>Fehlertext zur Liste der Logquellen (z. B. wenn keine vorhanden ist).</summary>
    [ObservableProperty]
    public partial string LogquellenFehler { get; set; } = "";

    /// <summary>Fehlertext zur Liste der Dienste.</summary>
    [ObservableProperty]
    public partial string DiensteFehler { get; set; } = "";

    /// <summary>Fehlertext, falls das Speichern selbst scheitert (z. B. Schreibfehler).</summary>
    [ObservableProperty]
    public partial string SpeicherFehler { get; set; } = "";

    /// <summary>Ob das Profil alle Regeln erfüllt.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SpeichernCommand))]
    public partial bool IstGueltig { get; set; }

    partial void OnNameChanged(string value) => Validiere();

    [RelayCommand]
    private void LogquelleHinzufuegen() => FuegeHinzu(new LogquelleZeile(new Logquelle()));

    [RelayCommand]
    private void LogquelleEntfernen(LogquelleZeile zeile) => Entferne(Logquellen, zeile);

    [RelayCommand]
    private void DienstHinzufuegen() => FuegeHinzu(new DienstZeile(new Dienst()));

    [RelayCommand]
    private void DienstEntfernen(DienstZeile zeile) => Entferne(Dienste, zeile);

    [RelayCommand]
    private void AufgabeHinzufuegen() => FuegeHinzu(new AufgabeZeile(new GeplanteAufgabe()));

    [RelayCommand]
    private void AufgabeEntfernen(AufgabeZeile zeile) => Entferne(Aufgaben, zeile);

    [RelayCommand(CanExecute = nameof(IstGueltig))]
    private async Task SpeichernAsync()
    {
        try
        {
            var profil = BaueProfil();
            var ergebnis = await _service.SpeichernAsync(profil, _bisherigerName);
            if (!ergebnis.IstGueltig)
            {
                VerteileFehler(ergebnis);
                return;
            }

            Gespeichert = profil;
            SchliessenAngefordert?.Invoke(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SpeicherFehler = $"Das Profil konnte nicht gespeichert werden: {ex.Message}";
        }
    }

    [RelayCommand]
    private void Abbrechen() => SchliessenAngefordert?.Invoke(false);

    private void FuegeHinzu(LogquelleZeile z) { z.Geaendert += Validiere; Logquellen.Add(z); Validiere(); }

    private void FuegeHinzu(DienstZeile z) { z.Geaendert += Validiere; Dienste.Add(z); Validiere(); }

    private void FuegeHinzu(AufgabeZeile z) { z.Geaendert += Validiere; Aufgaben.Add(z); }

    private void Entferne<T>(ObservableCollection<T> liste, T zeile) where T : ProfilZeile
    {
        zeile.Geaendert -= Validiere;
        liste.Remove(zeile);
        Validiere();
    }

    private Systemprofil BaueProfil() => _vorlage with
    {
        Name = Name.Trim(),
        Beschreibung = Beschreibung,
        Logquellen = [.. Logquellen.Select(z => z.ZuLogquelle())],
        Dienste = [.. Dienste.Select(z => z.ZuDienst())],
        Aufgaben = [.. Aufgaben.Where(z => !string.IsNullOrWhiteSpace(z.Name)).Select(z => z.ZuAufgabe())],
    };

    private void Validiere()
    {
        // Wird auch während des Konstruktors aufgerufen, bevor alle Listen befüllt sind.
        SpeicherFehler = "";
        VerteileFehler(_service.Pruefen(BaueProfil(), _bisherigerName));
    }

    private void VerteileFehler(Validierungsergebnis ergebnis)
    {
        string Erste(string feld) => ergebnis.MeldungenFuer(feld).FirstOrDefault() ?? "";

        NameFehler = Erste("Name");
        LogquellenFehler = Erste("Logquellen");
        DiensteFehler = Erste("Dienste");

        for (var i = 0; i < Logquellen.Count; i++)
        {
            Logquellen[i].PfadFehler = Erste($"Logquellen[{i}].Pfad");
            Logquellen[i].DateimusterFehler = Erste($"Logquellen[{i}].Dateimuster");
        }

        for (var i = 0; i < Dienste.Count; i++)
        {
            Dienste[i].DienstnameFehler = Erste($"Dienste[{i}].Dienstname");
        }

        IstGueltig = ergebnis.IstGueltig;
    }
}
