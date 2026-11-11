using Morphix.Core.Domaene;

namespace Morphix.Core.Schnittstellen;

/// <summary>Die Datenquellen, mit denen eine Diagnose für ein Profil arbeitet.</summary>
/// <param name="LogLeser">Liest Logdateien.</param>
/// <param name="Dienste">Fragt Windows-Dienste ab und steuert sie.</param>
/// <param name="Aufgaben">Liest die Aufgabenplanung.</param>
/// <param name="Ereignisse">Liest die Ereignisanzeige.</param>
public sealed record Datenquellen(
    ILogLeser LogLeser,
    IDienstGateway Dienste,
    IAufgabenGateway Aufgaben,
    IEreignisLeser Ereignisse);

/// <summary>
/// Liefert zu einem Profil die passenden Datenquellen: echte Windows-Adapter oder, bei
/// <see cref="Systemprofil.IstDemo"/>, simulierte. Der Demo-Modus ist damit ein Austausch der
/// Adapter und kein Sonderfall im Code der Services.
/// </summary>
public interface IDatenquellenFabrik
{
    /// <summary>Erzeugt die Datenquellen für das Profil.</summary>
    Datenquellen Erzeugen(Systemprofil profil);
}
