using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Leer;

// Platzhalter für Windows-Adapter, die noch nicht umgesetzt sind (M5: Dienste, M7: Aufgaben und Ereignisse).
// Sie liefern leere Ergebnisse, damit eine Analyse echter Profile schon mit den Logdateien funktioniert.
// Sobald der echte Adapter existiert, wird er in der Fabrik an ihre Stelle gesetzt und der Platzhalter gelöscht.

/// <summary>Platzhalter für den Dienst-Adapter: liefert keine Dienste und führt nichts aus.</summary>
public sealed class LeeresDienstGateway : IDienstGateway
{
    /// <inheritdoc />
    public Task<IReadOnlyList<Dienst>> AbfragenAsync(IEnumerable<Dienst> dienste, CancellationToken abbruch = default) =>
        Task.FromResult<IReadOnlyList<Dienst>>([]);

    /// <inheritdoc />
    /// <exception cref="NotSupportedException">Dienste können erst mit dem Windows-Adapter (M5) gesteuert werden.</exception>
    public Task<Dienst> AusfuehrenAsync(Dienst dienst, DienstAktionsart aktion, CancellationToken abbruch = default) =>
        throw new NotSupportedException("Die Dienststeuerung für echte Systeme ist noch nicht implementiert (Meilenstein M5).");
}

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
