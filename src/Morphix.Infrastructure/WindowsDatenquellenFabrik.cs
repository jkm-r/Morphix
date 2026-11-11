using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure.Demo;

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

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Für echte Profile gibt es die Windows-Adapter noch nicht (M4 bis M7).</exception>
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

        throw new NotSupportedException("Die Windows-Adapter für echte Systeme sind noch nicht implementiert (Meilensteine M4 bis M7).");
    }
}
