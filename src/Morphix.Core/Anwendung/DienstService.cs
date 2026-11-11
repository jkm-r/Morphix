using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Core.Anwendung;

/// <summary>Ergebnis einer Dienstaktion.</summary>
/// <param name="Erfolgreich">Ob die Aktion ausgeführt wurde.</param>
/// <param name="Meldung">Deutsche Meldung für den Anwender (Erfolg oder Grund der Ablehnung).</param>
/// <param name="Dienst">Der Dienst nach der Aktion; <c>null</c>, wenn nichts ausgeführt wurde.</param>
public sealed record DienstAktionsErgebnis(bool Erfolgreich, string Meldung, Dienst? Dienst);

/// <summary>
/// Prozesse P4 und P5: Dienststatus abfragen und Dienste steuern. Die Regel GR03 wird hier geprüft und
/// nicht nur in der Oberfläche: Gesteuert wird nur, wenn der Dienst im Profil steht, Administratorrechte
/// vorliegen und der Anwender bestätigt hat. Ob echte oder simulierte Dienste gesteuert werden (GR04),
/// entscheidet die Fabrik; der Service kennt den Unterschied nicht.
/// </summary>
public sealed class DienstService
{
    /// <summary>Hinweis, der an die Meldung angehängt wird, wenn das Protokoll nicht geschrieben werden konnte.</summary>
    public const string ProtokollFehlerHinweis = "Hinweis: Die Aktion konnte nicht im Aktionsprotokoll vermerkt werden.";

    private readonly IDatenquellenFabrik _fabrik;
    private readonly IAktionsProtokoll? _protokoll;
    private readonly TimeProvider _zeit;
    private readonly Func<string> _benutzer;

    /// <summary>Erzeugt den Service.</summary>
    /// <param name="fabrik">Liefert die Datenquellen je Profil.</param>
    /// <param name="protokoll">Protokoll der Dienstaktionen (US16); ohne Angabe wird nicht protokolliert.</param>
    /// <param name="zeit">Uhr für den Zeitpunkt der Einträge; ohne Angabe die Systemuhr.</param>
    /// <param name="benutzer">Liefert den Namen des ausführenden Benutzers; ohne Angabe der angemeldete Windows-Benutzer.</param>
    public DienstService(
        IDatenquellenFabrik fabrik,
        IAktionsProtokoll? protokoll = null,
        TimeProvider? zeit = null,
        Func<string>? benutzer = null)
    {
        _fabrik = fabrik;
        _protokoll = protokoll;
        _zeit = zeit ?? TimeProvider.System;
        _benutzer = benutzer ?? (() => Environment.UserName);
    }

    /// <summary>Liest die jüngsten Einträge des Aktionsprotokolls, neueste zuerst (US16).</summary>
    /// <param name="maximal">Höchstzahl der Einträge.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    public async Task<IReadOnlyList<Dienstaktion>> ProtokollLesenAsync(int maximal = 100, CancellationToken abbruch = default) =>
        _protokoll is null ? [] : await _protokoll.LeseAsync(maximal, abbruch);

    /// <summary>Fragt Status und Starttyp aller Profildienste ab.</summary>
    public Task<IReadOnlyList<Dienst>> AbfragenAsync(Systemprofil profil, CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(profil);
        return _fabrik.Erzeugen(profil).Dienste.AbfragenAsync(profil.Dienste, abbruch);
    }

    /// <summary>Ob Dienste für dieses Profil gesteuert werden dürfen (Administratorrechte, GR03).</summary>
    public bool HatAdminrechte(Systemprofil profil)
    {
        ArgumentNullException.ThrowIfNull(profil);
        return _fabrik.Erzeugen(profil).Rechte.HatAdminrechte();
    }

    /// <summary>Führt eine Aktion auf einem Dienst aus, wenn GR03 erfüllt ist.</summary>
    /// <param name="profil">Das aktive Profil; nur dessen Dienste dürfen gesteuert werden.</param>
    /// <param name="dienst">Der Dienst (es zählt der Dienstname).</param>
    /// <param name="aktion">Starten, Stoppen oder Neustarten.</param>
    /// <param name="bestaetigt">Ob der Anwender die Aktion bestätigt hat.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    /// <remarks>
    /// Jede Aktion landet im Aktionsprotokoll (US16): ausgeführte ebenso wie abgelehnte (z. B. ohne Administratorrechte) und
    /// fehlgeschlagene. Ein Fehler beim Schreiben des Protokolls ändert nichts am Ergebnis der Aktion, wird aber in der Meldung genannt.
    /// </remarks>
    public async Task<DienstAktionsErgebnis> AusfuehrenAsync(
        Systemprofil profil,
        Dienst dienst,
        DienstAktionsart aktion,
        bool bestaetigt,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(profil);
        ArgumentNullException.ThrowIfNull(dienst);

        DienstAktionsErgebnis ergebnis;
        try
        {
            ergebnis = await FuehreAusAsync(profil, dienst, aktion, bestaetigt, abbruch);
        }
        catch (Exception ex)
        {
            // Auch ein Abbruch oder ein unerwarteter Fehler kann den Dienst schon berührt haben und gehört ins Protokoll.
            var meldung = ex is OperationCanceledException ? "Die Aktion wurde abgebrochen." : $"Unerwarteter Fehler: {ex.Message}";
            await ProtokolliereAsync(profil, dienst, aktion, false, meldung);
            throw;
        }

        var hinweis = await ProtokolliereAsync(profil, dienst, aktion, ergebnis.Erfolgreich, ergebnis.Meldung);
        return hinweis is null ? ergebnis : ergebnis with { Meldung = $"{ergebnis.Meldung} {hinweis}" };
    }

    // Schreibt den Protokolleintrag. Liefert einen Hinweistext, wenn das Schreiben nicht möglich war, sonst null.
    private async Task<string?> ProtokolliereAsync(Systemprofil profil, Dienst dienst, DienstAktionsart aktion, bool erfolgreich, string meldung)
    {
        if (_protokoll is null)
        {
            return null;
        }

        try
        {
            await _protokoll.ProtokolliereAsync(new Dienstaktion(_zeit.GetLocalNow().DateTime, aktion, _benutzer(), erfolgreich)
            {
                Dienstname = dienst.Dienstname,
                Profil = profil.Name,
                Simuliert = profil.IstDemo,
                Meldung = meldung,
            });
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ProtokollFehlerHinweis;
        }
    }

    private async Task<DienstAktionsErgebnis> FuehreAusAsync(
        Systemprofil profil,
        Dienst dienst,
        DienstAktionsart aktion,
        bool bestaetigt,
        CancellationToken abbruch)
    {

        // GR03, Teil 1: nur Dienste aus dem aktiven Profil.
        var imProfil = profil.Dienste.FirstOrDefault(
            d => string.Equals(d.Dienstname, dienst.Dienstname, StringComparison.OrdinalIgnoreCase));
        if (imProfil is null)
        {
            return Abgelehnt("Der Dienst gehört nicht zum Profil und wird nicht gesteuert.");
        }

        var quellen = _fabrik.Erzeugen(profil);

        // GR03, Teil 2: Administratorrechte.
        if (!quellen.Rechte.HatAdminrechte())
        {
            return Abgelehnt("Für die Dienststeuerung sind Administratorrechte erforderlich.");
        }

        // GR03, Teil 3: ausdrückliche Bestätigung.
        if (!bestaetigt)
        {
            return Abgelehnt("Die Aktion wurde nicht bestätigt.");
        }

        try
        {
            var ergebnis = await quellen.Dienste.AusfuehrenAsync(imProfil, aktion, abbruch);
            return new DienstAktionsErgebnis(true, $"Dienst „{imProfil.Dienstname}“ wurde {Partizip(aktion)}.", ergebnis);
        }
        catch (DienstAktionFehlgeschlagenException ex)
        {
            return Abgelehnt(ex.Message);
        }
    }

    private static DienstAktionsErgebnis Abgelehnt(string grund) => new(false, grund, null);

    private static string Partizip(DienstAktionsart aktion) => aktion switch
    {
        DienstAktionsart.Starten => "gestartet",
        DienstAktionsart.Stoppen => "gestoppt",
        _ => "neu gestartet",
    };
}
