using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Leer;

// Platzhalter für Windows-Adapter, die noch nicht umgesetzt sind (M7: Aufgaben und Ereignisse).
// Sie liefern leere Ergebnisse, damit eine Analyse echter Profile schon mit den Logdateien funktioniert.
// Sobald der echte Adapter existiert, wird er in der Fabrik an ihre Stelle gesetzt und der Platzhalter gelöscht.

/// <summary>Platzhalter für den Aufgabenplanungs-Adapter: liefert keine Aufgaben.</summary>
public sealed class LeeresAufgabenGateway : IAufgabenGateway
{
    /// <inheritdoc />
    public Task<IReadOnlyList<GeplanteAufgabe>> AbfragenAsync(IEnumerable<GeplanteAufgabe> aufgaben, CancellationToken abbruch = default) =>
        Task.FromResult<IReadOnlyList<GeplanteAufgabe>>([]);
}

/// <summary>Platzhalter für den Ereignisanzeige-Adapter: liefert keine Ereignisse.</summary>
public sealed class LeererEreignisLeser : IEreignisLeser
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Logeintrag>> LesenAsync(Logquelle quelle, DateTime von, DateTime bis, CancellationToken abbruch = default) =>
        Task.FromResult<IReadOnlyList<Logeintrag>>([]);
}
