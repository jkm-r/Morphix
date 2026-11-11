using Morphix.Core.Domaene;

namespace Morphix.Core.Schnittstellen;

/// <summary>
/// Protokoll der in Morphix ausgeführten Dienstaktionen (Could, US16): jede Aktion mit Zeit, Benutzer und Ergebnis,
/// damit Eingriffe nachvollziehbar sind. Implementierungen liegen in der Infrastruktur.
/// </summary>
public interface IAktionsProtokoll
{
    /// <summary>Hängt einen Eintrag an das Protokoll an. Vorhandene Einträge werden nie verändert oder gelöscht.</summary>
    /// <exception cref="IOException">Das Protokoll konnte nicht geschrieben werden.</exception>
    Task ProtokolliereAsync(Dienstaktion aktion, CancellationToken abbruch = default);

    /// <summary>Liest die jüngsten Einträge, neueste zuerst. Unlesbare Zeilen werden übersprungen.</summary>
    /// <param name="maximal">Höchstzahl der gelieferten Einträge.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    Task<IReadOnlyList<Dienstaktion>> LeseAsync(int maximal = 100, CancellationToken abbruch = default);
}
