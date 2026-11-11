using Morphix.Core.Domaene;

namespace Morphix.Core.Schnittstellen;

/// <summary>Liest geplante Aufgaben aus der Windows-Aufgabenplanung (Should, US10).</summary>
public interface IAufgabenGateway
{
    /// <summary>
    /// Ergänzt die Aufgaben um letzten Lauf, Ergebnis und nächsten Lauf. Aufgaben, die das System nicht
    /// kennt, werden unverändert zurückgegeben.
    /// </summary>
    Task<IReadOnlyList<GeplanteAufgabe>> AbfragenAsync(IEnumerable<GeplanteAufgabe> aufgaben, CancellationToken abbruch = default);
}
