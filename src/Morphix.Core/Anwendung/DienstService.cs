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
    private readonly IDatenquellenFabrik _fabrik;

    /// <summary>Erzeugt den Service.</summary>
    public DienstService(IDatenquellenFabrik fabrik)
    {
        _fabrik = fabrik;
    }

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
    public async Task<DienstAktionsErgebnis> AusfuehrenAsync(
        Systemprofil profil,
        Dienst dienst,
        DienstAktionsart aktion,
        bool bestaetigt,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(profil);
        ArgumentNullException.ThrowIfNull(dienst);

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
