using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Morphix.App.Dialoge;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;

namespace Morphix.App.ViewModels;

/// <summary>
/// ViewModel der Ansicht „Bericht“ (P7): zeigt die Vorschau des Support-Berichts, bietet die Option
/// „Sensible Daten maskieren“ und exportiert Bericht und Logs als ZIP.
/// </summary>
public sealed partial class BerichtViewModel : ObservableViewModel
{
    private readonly BerichtService _service;
    private readonly IExportDialog _exportDialog;
    private readonly IBestaetigungsDialog _bestaetigung;
    private Diagnose? _diagnose;
    private bool _setzeMaskierungZurueck;
    private int _vorschauNummer;

    /// <summary>Erzeugt das ViewModel.</summary>
    public BerichtViewModel(BerichtService service, IExportDialog exportDialog, IBestaetigungsDialog bestaetigung)
    {
        _service = service;
        _exportDialog = exportDialog;
        _bestaetigung = bestaetigung;
    }

    /// <summary>Ob eine abgeschlossene Diagnose vorliegt, aus der ein Bericht entstehen kann (GR09).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportierenCommand))]
    [NotifyPropertyChangedFor(nameof(LeerHinweis))]
    public partial bool HatDiagnose { get; set; }

    /// <summary>Der Bericht als HTML für die Vorschau; leer, solange keine Diagnose vorliegt.</summary>
    [ObservableProperty]
    public partial string Vorschau { get; set; } = "";

    /// <summary>Option „Sensible Daten maskieren“ (GR10): standardmäßig an, nur bewusst abschaltbar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaskierungHinweis))]
    public partial bool Maskieren { get; set; } = true;

    /// <summary>Warnung, solange die Maskierung ausgeschaltet ist.</summary>
    public string MaskierungHinweis => Maskieren
        ? ""
        : "Achtung: Der Bericht und die Logs im ZIP enthalten Benutzernamen und IP-Adressen im Klartext.";

    /// <summary>Hinweis, solange noch keine Analyse vorliegt.</summary>
    public string LeerHinweis => HatDiagnose ? "" : "Noch keine Analyse durchgeführt. Wähle ein Profil und einen Zeitraum und starte die Analyse.";

    /// <summary>Meldung zum letzten Export oder zu einem Fehler.</summary>
    [ObservableProperty]
    public partial string Meldung { get; set; } = "";

    /// <summary>Ob die Meldung einen Fehler beschreibt (für die Darstellung).</summary>
    [ObservableProperty]
    public partial bool MeldungIstFehler { get; set; }

    /// <summary>Ob gerade exportiert wird.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportierenCommand))]
    public partial bool IstExportLaeuft { get; set; }

    /// <summary>
    /// Übernimmt eine neue Diagnose (<c>null</c> setzt die Ansicht zurück). Die Maskierung wird dabei wieder
    /// eingeschaltet: Sie lässt sich nur pro Bericht bewusst abschalten (GR10).
    /// </summary>
    public void Zeige(Diagnose? diagnose)
    {
        _diagnose = diagnose is { IstAbgeschlossen: true } ? diagnose : null;
        Meldung = "";
        MeldungIstFehler = false;

        if (!Maskieren)
        {
            _setzeMaskierungZurueck = true;
            try
            {
                Maskieren = true;
            }
            finally
            {
                _setzeMaskierungZurueck = false;
            }
        }

        HatDiagnose = _diagnose is not null;
        _ = AktualisiereVorschauAsync();
    }

    partial void OnMaskierenChanged(bool value)
    {
        if (_setzeMaskierungZurueck)
        {
            return;
        }

        if (!value && !_bestaetigung.Bestaetigen(
                "Maskierung ausschalten",
                "Ohne Maskierung stehen Benutzernamen, IP-Adressen und der Rechnername im Klartext im Bericht und in den Logs im ZIP. Maskierung wirklich ausschalten?"))
        {
            // Bewusst abschalten heißt: erst nach Bestätigung. Sonst bleibt die Maskierung an.
            _setzeMaskierungZurueck = true;
            try
            {
                Maskieren = true;
            }
            finally
            {
                _setzeMaskierungZurueck = false;
            }

            return;
        }

        _ = AktualisiereVorschauAsync();
    }

    // Die Erzeugung läuft nicht im UI-Thread (viele Fehlergruppen). Kommt währenddessen eine neuere Anforderung,
    // wird das ältere Ergebnis verworfen.
    private async Task AktualisiereVorschauAsync()
    {
        var nummer = ++_vorschauNummer;
        var diagnose = _diagnose;
        if (diagnose is null)
        {
            Vorschau = "";
            return;
        }

        var maskieren = Maskieren;
        try
        {
            var html = await Task.Run(() => _service.Erzeugen(diagnose, maskieren));
            if (nummer == _vorschauNummer)
            {
                Vorschau = html;
            }
        }
        catch (Exception ex)
        {
            if (nummer == _vorschauNummer)
            {
                Vorschau = "";
                Meldung = $"Die Vorschau konnte nicht erzeugt werden: {ex.Message}";
                MeldungIstFehler = true;
            }
        }
    }

    /// <summary>Erzeugt die Vorschau zur aktuellen Diagnose und Maskierung neu und wartet, bis sie vorliegt.</summary>
    public Task VorschauFertigAsync() => AktualisiereVorschauAsync();

    /// <summary>Exportiert Bericht und Logs als ZIP an einen vom Anwender gewählten Ort (US14).</summary>
    [RelayCommand(CanExecute = nameof(KannExportieren))]
    private async Task ExportierenAsync()
    {
        var diagnose = _diagnose;
        if (diagnose is null)
        {
            return;
        }

        var ziel = _exportDialog.ZielWaehlen(_service.Dateinamenvorschlag(diagnose));
        if (ziel is null)
        {
            return;
        }

        Meldung = "";
        MeldungIstFehler = false;

        var fehler = _service.PruefeZielpfad(ziel);
        if (fehler is not null)
        {
            Meldung = fehler;
            MeldungIstFehler = true;
            return;
        }

        IstExportLaeuft = true;
        try
        {
            var bericht = await _service.ExportierenAsync(diagnose, Maskieren, ziel);
            Meldung = $"Exportiert nach {bericht.Dateipfad}" + (bericht.Maskiert ? " (maskiert)." : " (ohne Maskierung).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Meldung = $"Der Export ist fehlgeschlagen: {ex.Message}";
            MeldungIstFehler = true;
        }
        finally
        {
            IstExportLaeuft = false;
        }
    }

    private bool KannExportieren() => HatDiagnose && !IstExportLaeuft;
}
