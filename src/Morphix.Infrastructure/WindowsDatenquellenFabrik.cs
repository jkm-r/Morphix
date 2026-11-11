using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure.Dateien;
using Morphix.Infrastructure.Demo;
using Morphix.Infrastructure.Dienste;
using Morphix.Infrastructure.Aufgaben;
using Morphix.Infrastructure.Ereignisse;

namespace Morphix.Infrastructure;

/// <summary>
/// Wählt je Profil echte Windows-Adapter oder die simulierten Demo-Adapter. Alle Services arbeiten
/// nur mit den Schnittstellen und kennen den Unterschied nicht.
/// </summary>
public sealed class WindowsDatenquellenFabrik : IDatenquellenFabrik
{
    private readonly string _demoOrdner;
    private readonly Lock _sperre = new();
    private readonly Dictionary<string, (string Dienstnamen, DemoDienstGateway Gateway)> _demoDienste = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDienstGateway _windowsDienste;
    private readonly IRechtePruefer _windowsRechte;

    /// <summary>Erzeugt die Fabrik.</summary>
    /// <param name="demoOrdner">Ordner <c>demo</c> mit den Demo-Logdateien.</param>
    /// <param name="windowsDienste">Dienst-Adapter für echte Profile; ohne Angabe der <see cref="WindowsDienstGateway"/>.</param>
    /// <param name="windowsRechte">Rechteprüfung für echte Profile; ohne Angabe der <see cref="WindowsRechtePruefer"/>.</param>
    public WindowsDatenquellenFabrik(string demoOrdner, IDienstGateway? windowsDienste = null, IRechtePruefer? windowsRechte = null)
    {
        _demoOrdner = demoOrdner;
        _windowsDienste = windowsDienste ?? new WindowsDienstGateway();
        _windowsRechte = windowsRechte ?? new WindowsRechtePruefer();
    }

    /// <summary>
    /// Liefert für Demo-Profile die Demo-Adapter, für echte Profile den <see cref="DateiLogLeser"/> und den
    /// <see cref="WindowsDienstGateway"/>, den <see cref="WindowsAufgabenGateway"/> und den <see cref="WindowsEreignisLeser"/> (alle rein lesend, außer der Dienststeuerung).
    /// </summary>
    public Datenquellen Erzeugen(Systemprofil profil)
    {
        ArgumentNullException.ThrowIfNull(profil);

        if (profil.IstDemo)
        {
            return new Datenquellen(
                new DemoLogLeser(_demoOrdner),
                DemoDienste(profil),
                new DemoAufgabenGateway(),
                new DemoEreignisLeser(),
                new DemoRechtePruefer());
        }

        return new Datenquellen(
            new DateiLogLeser(),
            _windowsDienste,
            new WindowsAufgabenGateway(),
            new WindowsEreignisLeser(),
            _windowsRechte);
    }

    // Das simulierte System bleibt für die Dauer der Sitzung bestehen: Ein gestarteter Demo-Dienst bleibt gestartet,
    // egal ob die Dienste-Ansicht oder die Analyse fragt. Ändert sich die Dienstliste des Profils, beginnt die Simulation neu.
    private DemoDienstGateway DemoDienste(Systemprofil profil)
    {
        var namen = string.Join('|', profil.Dienste.Select(d => d.Dienstname));
        lock (_sperre)
        {
            if (!_demoDienste.TryGetValue(profil.Name, out var eintrag) || eintrag.Dienstnamen != namen)
            {
                eintrag = (namen, new DemoDienstGateway(profil.Dienste));
                _demoDienste[profil.Name] = eintrag;
            }

            return eintrag.Gateway;
        }
    }
}
