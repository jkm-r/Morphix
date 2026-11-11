using Morphix.Core.Domaene;

namespace Morphix.Core.Schnittstellen;

/// <summary>Liest Einträge der Windows-Ereignisanzeige (Should, US06).</summary>
public interface IEreignisLeser
{
    /// <summary>Liest die Ereignisse eines Protokolls im Zeitraum (nur Warnungen, Fehler und kritische Ereignisse).</summary>
    /// <param name="quelle">Die Logquelle vom Typ Ereignisanzeige; <see cref="Logquelle.Pfad"/> ist der Protokollname.</param>
    /// <param name="von">Beginn des Zeitraums.</param>
    /// <param name="bis">Ende des Zeitraums.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    Task<IReadOnlyList<Logeintrag>> LesenAsync(Logquelle quelle, DateTime von, DateTime bis, CancellationToken abbruch = default);
}
