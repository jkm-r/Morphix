using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Morphix.App.Dialoge;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;

namespace Morphix.App.ViewModels;

/// <summary>Eine Zeile der Dienste-Tabelle: der Dienst samt Anzeigetexten und Hervorhebung (US09).</summary>
public sealed record DienstAnzeige(Dienst Dienst)
{
    /// <summary>Technischer Dienstname.</summary>
    public string Dienstname => Dienst.Dienstname;

    /// <summary>Anzeigename, sonst der Dienstname.</summary>
    public string Name => string.IsNullOrWhiteSpace(Dienst.Anzeigename) ? Dienst.Dienstname : Dienst.Anzeigename;

    /// <summary>Kennzeichnung kritischer Dienste.</summary>
    public string Kritisch => Dienst.IstKritisch ? "Ja" : "";

    /// <summary>Status als deutscher Text.</summary>
    public string StatusText => Dienst.Status.Text();

    /// <summary>Starttyp als deutscher Text.</summary>
    public string StarttypText => Dienst.Starttyp.Text();

    /// <summary>US09: ein kritischer Dienst, der nicht läuft, wird farblich hervorgehoben.</summary>
    public bool IstKritischGestoppt => Dienst.IstKritisch && Dienst.Status == DienstStatus.Gestoppt;

    /// <summary>Hinweis zur Zeile, z. B. bei einem Dienst, den es auf dem System nicht gibt.</summary>
    public string Hinweis => Dienst.Status == DienstStatus.NichtGefunden
        ? "Dieser Dienst ist auf dem System nicht vorhanden."
        : IstKritischGestoppt ? "Kritischer Dienst läuft nicht." : "";
}

/// <summary>Eine Zeile des Aktionsprotokolls (US16).</summary>
public sealed record AktionAnzeige(Dienstaktion Aktion)
{
    private static readonly System.Globalization.CultureInfo Kultur = System.Globalization.CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Zeitpunkt der Aktion.</summary>
    public string ZeitText => Aktion.Zeitpunkt.ToString("dd.MM.yyyy HH:mm:ss", Kultur);

    /// <summary>Ausführender Benutzer.</summary>
    public string Benutzer => Aktion.Benutzer;

    /// <summary>Art der Aktion als deutscher Text.</summary>
    public string AktionText => Aktion.Aktion switch
    {
        DienstAktionsart.Starten => "Starten",
        DienstAktionsart.Stoppen => "Stoppen",
        _ => "Neustarten",
    };

    /// <summary>Dienst und Profil, auf die sich die Aktion bezog.</summary>
    public string DienstText => string.IsNullOrEmpty(Aktion.Profil) ? Aktion.Dienstname : $"{Aktion.Dienstname} ({Aktion.Profil})";

    /// <summary>Ergebnis in einem Wort; simulierte Aktionen (Demo-Modus) sind gekennzeichnet.</summary>
    public string ErgebnisText => (Aktion.Erfolgreich ? "Erfolgreich" : "Fehlgeschlagen") + (Aktion.Simuliert ? " (simuliert)" : "");

    /// <summary>Meldung zum Ergebnis.</summary>
    public string Meldung => Aktion.Meldung;

    /// <summary>Ob die Aktion nicht erfolgreich war (rote Darstellung).</summary>
    public bool IstFehler => !Aktion.Erfolgreich;
}

/// <summary>
/// ViewModel der Ansicht „Dienste“ (P4, P5): zeigt Status und Starttyp der Profildienste, aktualisiert sie alle
/// 5 Sekunden und führt Start, Stopp und Neustart nach Bestätigung aus. Ohne Administratorrechte bleibt die
/// Ansicht lesend; die Regel GR03 prüft zusätzlich der <see cref="DienstService"/>.
/// </summary>
public sealed partial class DiensteViewModel : ObservableViewModel, IDisposable
{
    /// <summary>Abfrageintervall laut Spezifikation.</summary>
    public static readonly TimeSpan StandardIntervall = TimeSpan.FromSeconds(5);

    private readonly DienstService _service;
    private readonly IBestaetigungsDialog _dialog;
    private readonly TimeSpan _intervall;
    private Systemprofil? _profil;
    private CancellationTokenSource? _schleife;
    private bool _istAktiv;

    /// <summary>Erzeugt das ViewModel.</summary>
    /// <param name="service">Service für Abfrage und Steuerung.</param>
    /// <param name="dialog">Bestätigungsdialog.</param>
    /// <param name="intervall">Abfrageintervall; ohne Angabe 5 Sekunden. Tests übergeben ein kurzes Intervall.</param>
    public DiensteViewModel(DienstService service, IBestaetigungsDialog dialog, TimeSpan? intervall = null)
    {
        _service = service;
        _dialog = dialog;
        _intervall = intervall ?? StandardIntervall;
    }

    /// <summary>Die Dienste des aktiven Profils mit aktuellem Zustand.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DienstAnzeige> Dienste { get; set; } = [];

    /// <summary>Höchstzahl der angezeigten Protokolleinträge.</summary>
    public const int MaxProtokollEintraege = 100;

    /// <summary>Die jüngsten Dienstaktionen, neueste zuerst (US16).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<AktionAnzeige> Protokoll { get; set; } = [];

    /// <summary>Hinweis, wenn das Aktionsprotokoll nicht gelesen werden konnte, sonst leer.</summary>
    [ObservableProperty]
    public partial string ProtokollHinweis { get; set; } = "";

    /// <summary>Die gewählte Tabellenzeile.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartenCommand), nameof(StoppenCommand), nameof(NeustartenCommand))]
    public partial DienstAnzeige? AusgewaehlterDienst { get; set; }

    /// <summary>Ob Dienste gesteuert werden dürfen (Administratorrechte). Ohne Rechte sind die Schaltflächen gesperrt.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartenCommand), nameof(StoppenCommand), nameof(NeustartenCommand))]
    public partial bool HatAdminrechte { get; set; }

    /// <summary>Ob gerade eine Dienstaktion läuft.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartenCommand), nameof(StoppenCommand), nameof(NeustartenCommand))]
    public partial bool IstBeschaeftigt { get; set; }

    /// <summary>Meldung zur letzten Aktion oder Abfrage.</summary>
    [ObservableProperty]
    public partial string Meldung { get; set; } = "";

    /// <summary>US09: Warnhinweis, wenn kritische Dienste gestoppt sind.</summary>
    [ObservableProperty]
    public partial string KritischHinweis { get; set; } = "";

    /// <summary>Hinweis, wenn die Steuerung mangels Rechten gesperrt ist.</summary>
    public string RechteHinweis => HatAdminrechte || _profil is null
        ? ""
        : "Ohne Administratorrechte ist diese Ansicht nur lesend: Start, Stopp und Neustart sind gesperrt.";

    /// <summary>Hinweis, solange kein Profil gewählt ist.</summary>
    public string LeerHinweis => _profil is null ? "Wähle zuerst ein Profil." : "";

    /// <summary>
    /// Schaltet die automatische Aktualisierung (alle 5 Sekunden) ein, solange die Ansicht sichtbar ist.
    /// </summary>
    public bool IstAktiv
    {
        get => _istAktiv;
        set
        {
            if (_istAktiv == value)
            {
                return;
            }

            _istAktiv = value;
            if (value)
            {
                StarteSchleife();
            }
            else
            {
                BeendeSchleife();
            }
        }
    }

    /// <summary>Setzt das aktive Profil; die Liste wird neu geladen, wenn die Ansicht aktiv ist.</summary>
    public void SetzeProfil(Systemprofil? profil)
    {
        _profil = profil;
        Dienste = [];
        AusgewaehlterDienst = null;
        Meldung = "";
        KritischHinweis = "";
        HatAdminrechte = profil is not null && PruefeRechte(profil);
        OnPropertyChanged(nameof(RechteHinweis));
        OnPropertyChanged(nameof(LeerHinweis));

        if (_istAktiv && profil is not null)
        {
            _ = AktualisierenAsync();
        }
    }

    /// <summary>Fragt den Zustand der Dienste jetzt ab.</summary>
    public async Task AktualisierenAsync()
    {
        var profil = _profil;
        if (profil is null || IstBeschaeftigt)
        {
            return;
        }

        try
        {
            var dienste = await _service.AbfragenAsync(profil);
            if (!ReferenceEquals(profil, _profil))
            {
                return; // Das Profil wurde während der Abfrage gewechselt.
            }

            var name = AusgewaehlterDienst?.Dienstname;
            var zeilen = dienste.Select(d => new DienstAnzeige(d)).ToList();
            Dienste = zeilen;
            AusgewaehlterDienst = zeilen.FirstOrDefault(z => z.Dienstname == name);
            HatAdminrechte = PruefeRechte(profil);
            OnPropertyChanged(nameof(RechteHinweis));
            AktualisiereKritischHinweis(zeilen);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Meldung = $"Die Dienste konnten nicht abgefragt werden: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(KannStarten))]
    private Task StartenAsync() => FuehreAusAsync(DienstAktionsart.Starten);

    [RelayCommand(CanExecute = nameof(KannStoppen))]
    private Task StoppenAsync() => FuehreAusAsync(DienstAktionsart.Stoppen);

    [RelayCommand(CanExecute = nameof(KannNeustarten))]
    private Task NeustartenAsync() => FuehreAusAsync(DienstAktionsart.Neustarten);

    private bool KannSteuern => HatAdminrechte && !IstBeschaeftigt && AusgewaehlterDienst is not null;

    private bool KannStarten() => KannSteuern && AusgewaehlterDienst!.Dienst.Status is DienstStatus.Gestoppt or DienstStatus.Angehalten;

    private bool KannStoppen() => KannSteuern && AusgewaehlterDienst!.Dienst.Status is DienstStatus.Gestartet or DienstStatus.Angehalten;

    private bool KannNeustarten() => KannSteuern && AusgewaehlterDienst!.Dienst.Status is DienstStatus.Gestartet;

    private async Task FuehreAusAsync(DienstAktionsart aktion)
    {
        if (_profil is not { } profil || AusgewaehlterDienst is not { } zeile)
        {
            return;
        }

        var verb = aktion switch
        {
            DienstAktionsart.Starten => "starten",
            DienstAktionsart.Stoppen => "stoppen",
            _ => "neu starten",
        };

        // GR03: ohne ausdrückliche Bestätigung passiert nichts.
        if (!_dialog.Bestaetigen("Dienstaktion bestätigen", $"Dienst {zeile.Dienstname} wirklich {verb}?"))
        {
            Meldung = "Die Aktion wurde abgebrochen.";
            return;
        }

        IstBeschaeftigt = true;
        Meldung = $"Dienst {zeile.Dienstname} wird {(aktion == DienstAktionsart.Stoppen ? "gestoppt" : aktion == DienstAktionsart.Starten ? "gestartet" : "neu gestartet")} …";
        try
        {
            var ergebnis = await _service.AusfuehrenAsync(profil, zeile.Dienst, aktion, bestaetigt: true);
            Meldung = ergebnis.Meldung;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Meldung = $"Die Aktion ist fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IstBeschaeftigt = false;
        }

        await AktualisiereProtokollAsync();
        await AktualisierenAsync();
    }

    /// <summary>Liest das Aktionsprotokoll neu (US16).</summary>
    public async Task AktualisiereProtokollAsync()
    {
        try
        {
            Protokoll = [.. (await _service.ProtokollLesenAsync(MaxProtokollEintraege)).Select(a => new AktionAnzeige(a))];
            ProtokollHinweis = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ProtokollHinweis = $"Das Aktionsprotokoll konnte nicht gelesen werden: {ex.Message}";
        }
    }

    private bool PruefeRechte(Systemprofil profil)
    {
        try
        {
            return _service.HatAdminrechte(profil);
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void AktualisiereKritischHinweis(IReadOnlyList<DienstAnzeige> zeilen)
    {
        var gestoppt = zeilen.Where(z => z.IstKritischGestoppt).Select(z => z.Dienstname).ToList();
        KritischHinweis = gestoppt.Count == 0
            ? ""
            : gestoppt.Count == 1
                ? $"Achtung: Der kritische Dienst {gestoppt[0]} läuft nicht."
                : $"Achtung: {gestoppt.Count} kritische Dienste laufen nicht: {string.Join(", ", gestoppt)}.";
    }

    private void StarteSchleife()
    {
        if (_schleife is not null)
        {
            return;
        }

        _schleife = new CancellationTokenSource();
        _ = SchleifeAsync(_schleife.Token);
    }

    private void BeendeSchleife()
    {
        _schleife?.Cancel();
        _schleife?.Dispose();
        _schleife = null;
    }

    private async Task SchleifeAsync(CancellationToken abbruch)
    {
        try
        {
            await AktualisiereProtokollAsync();
            await AktualisierenAsync();
            using var timer = new PeriodicTimer(_intervall);
            while (await timer.WaitForNextTickAsync(abbruch))
            {
                await AktualisierenAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // Normales Ende, wenn die Ansicht verlassen wird.
        }
    }

    /// <summary>Beendet die automatische Aktualisierung.</summary>
    public void Dispose() => BeendeSchleife();
}
