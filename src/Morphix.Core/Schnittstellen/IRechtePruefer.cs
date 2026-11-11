namespace Morphix.Core.Schnittstellen;

/// <summary>Prüft, ob die Anwendung mit Administratorrechten läuft (Grundlage für GR03).</summary>
public interface IRechtePruefer
{
    /// <summary><c>true</c>, wenn der aktuelle Benutzer Administratorrechte hat.</summary>
    bool HatAdminrechte();
}
