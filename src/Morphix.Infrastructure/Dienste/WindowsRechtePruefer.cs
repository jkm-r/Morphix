using System.Security.Principal;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Dienste;

/// <summary>Prüft, ob der aktuelle Prozess mit Administratorrechten (erhöht) läuft.</summary>
public sealed class WindowsRechtePruefer : IRechtePruefer
{
    private readonly Func<bool> _pruefung;

    /// <summary>Erzeugt den Prüfer.</summary>
    /// <param name="pruefung">Eigene Prüfung für Tests; ohne Angabe wird <see cref="WindowsPrincipal"/> verwendet.</param>
    public WindowsRechtePruefer(Func<bool>? pruefung = null)
    {
        _pruefung = pruefung ?? IstAdministrator;
    }

    /// <inheritdoc />
    public bool HatAdminrechte() => _pruefung();

    private static bool IstAdministrator()
    {
        using var identitaet = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identitaet).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
