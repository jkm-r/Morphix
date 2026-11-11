using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure.Dateien;
using Morphix.Infrastructure.Demo;
using Morphix.Infrastructure.Leer;

namespace Morphix.Infrastructure;

/// <summary>
/// Wählt je Profil echte Windows-Adapter oder die simulierten Demo-Adapter. Alle Services arbeiten
/// nur mit den Schnittstellen und kennen den Unterschied nicht.
/// </summary>
public sealed class WindowsDatenquellenFabrik : IDatenquellenFabrik
{
    private readonly string _demoOrdner;

    /// <summary>Erzeugt die Fabrik.</summary>
    /// <param name="demoOrdner">Ordner <c>demo</c> mit den Demo-Logdateien.</param>
    public WindowsDatenquellenFabrik(string demoOrdner)
    {
        _demoOrdner = demoOrdner;
    }

    /// <summary>
    /// Liefert für Demo-Profile die Demo-Adapter. Für echte Profile gibt es den <see cref="DateiLogLeser"/>;
    /// Dienste, Aufgaben und Ereignisse liefern bis zu den Meilensteinen M5 und M7 leere Ergebnisse.
    /// </summary>
    public Datenquellen Erzeugen(Systemprofil profil)
    {
        ArgumentNullException.ThrowIfNull(profil);

        if (profil.IstDemo)
        {
            return new Datenquellen(
                new DemoLogLeser(_demoOrdner),
                new DemoDienstGateway(profil.Dienste),
                new DemoAufgabenGateway(),
                new DemoEreignisLeser());
        }

        return new Datenquellen(
            new DateiLogLeser(),
            new LeeresDienstGateway(),
            new LeeresAufgabenGateway(),
            new LeererEreignisLeser());
    }
}
