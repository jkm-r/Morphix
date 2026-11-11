using Morphix.Core.Domaene;

namespace Morphix.Core.Schnittstellen;

/// <summary>Speichert und lädt Systemprofile (Repository-Muster).</summary>
public interface IProfilRepository
{
    /// <summary>Lädt alle gespeicherten Profile. Unlesbare Profildateien werden übersprungen.</summary>
    Task<IReadOnlyList<Systemprofil>> LadenAlleAsync(CancellationToken abbruch = default);

    /// <summary>
    /// Speichert ein Profil. Wurde es umbenannt, wird das Profil unter dem alten Namen entfernt.
    /// </summary>
    /// <param name="profil">Das zu speichernde Profil.</param>
    /// <param name="alterName">Bisheriger Name bei einer Umbenennung, sonst <c>null</c>.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    Task SpeichernAsync(Systemprofil profil, string? alterName = null, CancellationToken abbruch = default);

    /// <summary>Löscht das Profil mit dem Namen, falls vorhanden.</summary>
    Task LoeschenAsync(string name, CancellationToken abbruch = default);

    /// <summary>Name des zuletzt genutzten Profils oder <c>null</c>.</summary>
    Task<string?> LadeZuletztGenutztesAsync(CancellationToken abbruch = default);

    /// <summary>Merkt sich das zuletzt genutzte Profil.</summary>
    Task MerkeZuletztGenutztesAsync(string name, CancellationToken abbruch = default);

    /// <summary>Lädt das mitgelieferte Demo-Profil oder <c>null</c>, wenn es nicht vorhanden ist.</summary>
    Task<Systemprofil?> LadeDemoProfilAsync(CancellationToken abbruch = default);
}
