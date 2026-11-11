using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Core.Anwendung;

/// <summary>Ergebnis des Starts: alle Profile und das zu aktivierende Profil.</summary>
/// <param name="Profile">Alle vorhandenen Profile.</param>
/// <param name="Aktiv">Das zuletzt genutzte Profil, sonst das erste; <c>null</c>, wenn es kein Profil gibt.</param>
/// <param name="DemoProfilAngelegt"><c>true</c>, wenn beim ersten Start das Demo-Profil angelegt wurde.</param>
public sealed record ProfilStart(IReadOnlyList<Systemprofil> Profile, Systemprofil? Aktiv, bool DemoProfilAngelegt);

/// <summary>Prozess P1: Profile laden, validieren (GR02) und speichern.</summary>
public sealed class ProfilService
{
    private readonly IProfilRepository _repository;
    private readonly ProfilValidator _validator;
    private List<Systemprofil> _profile = [];

    /// <summary>Erzeugt den Service.</summary>
    public ProfilService(IProfilRepository repository, ProfilValidator validator)
    {
        _repository = repository;
        _validator = validator;
    }

    /// <summary>Die zuletzt geladenen oder gespeicherten Profile.</summary>
    public IReadOnlyList<Systemprofil> Profile => _profile;

    /// <summary>
    /// Lädt alle Profile. Gibt es noch keines (erster Start), wird das mitgelieferte Demo-Profil angelegt.
    /// </summary>
    public async Task<ProfilStart> StartenAsync(CancellationToken abbruch = default)
    {
        var profile = (await _repository.LadenAlleAsync(abbruch)).ToList();
        var demoAngelegt = false;

        if (profile.Count == 0)
        {
            var demo = await _repository.LadeDemoProfilAsync(abbruch);
            if (demo is not null)
            {
                await _repository.SpeichernAsync(demo, null, abbruch);
                profile.Add(demo);
                demoAngelegt = true;
            }
        }

        _profile = [.. profile.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];

        var letzter = await _repository.LadeZuletztGenutztesAsync(abbruch);
        var aktiv = _profile.FirstOrDefault(p => string.Equals(p.Name, letzter, StringComparison.OrdinalIgnoreCase))
                    ?? _profile.FirstOrDefault();
        return new ProfilStart(_profile, aktiv, demoAngelegt);
    }

    /// <summary>
    /// Prüft ein Profil gegen GR02 und die Feldregeln.
    /// </summary>
    /// <param name="profil">Das Profil.</param>
    /// <param name="bisherigerName">Name des bearbeiteten Profils vor der Änderung (<c>null</c> bei einem neuen Profil); er zählt nicht als Duplikat.</param>
    public Validierungsergebnis Pruefen(Systemprofil profil, string? bisherigerName)
    {
        var andere = _profile
            .Select(p => p.Name)
            .Where(n => !string.Equals(n, bisherigerName, StringComparison.OrdinalIgnoreCase));
        return _validator.Pruefen(profil, andere);
    }

    /// <summary>Validiert und speichert ein Profil. Bei Verstößen wird nichts gespeichert.</summary>
    /// <returns>Das Prüfergebnis; gespeichert wurde nur, wenn es gültig ist.</returns>
    public async Task<Validierungsergebnis> SpeichernAsync(Systemprofil profil, string? bisherigerName, CancellationToken abbruch = default)
    {
        var ergebnis = Pruefen(profil, bisherigerName);
        if (!ergebnis.IstGueltig)
        {
            return ergebnis;
        }

        await _repository.SpeichernAsync(profil, bisherigerName, abbruch);

        _profile.RemoveAll(p => string.Equals(p.Name, bisherigerName ?? profil.Name, StringComparison.OrdinalIgnoreCase));
        _profile.Add(profil);
        _profile = [.. _profile.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)];
        return ergebnis;
    }

    /// <summary>Merkt sich das Profil als zuletzt genutzt.</summary>
    public Task MerkeAktivAsync(Systemprofil profil, CancellationToken abbruch = default) =>
        _repository.MerkeZuletztGenutztesAsync(profil.Name, abbruch);
}
