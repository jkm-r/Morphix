using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Demo;

/// <summary>
/// Rechteprüfung im Demo-Modus: Da nur simulierte Dienste gesteuert werden (GR04), sind keine
/// Administratorrechte nötig. So lässt sich die Dienststeuerung auch ohne erhöhte Rechte vorführen.
/// </summary>
public sealed class DemoRechtePruefer : IRechtePruefer
{
    /// <inheritdoc />
    public bool HatAdminrechte() => true;
}
