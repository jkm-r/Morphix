using Morphix.Core.Domaene;

namespace Morphix.Core.Schnittstellen;

/// <summary>Liest die Logdateien einer Logquelle (Port; Implementierungen liegen in der Infrastruktur).</summary>
public interface ILogLeser
{
    /// <summary>Liest alle Einträge der Quelle im Zeitraum.</summary>
    /// <param name="quelle">Die Logquelle (Typ Datei).</param>
    /// <param name="von">Beginn des Zeitraums.</param>
    /// <param name="bis">Ende des Zeitraums.</param>
    /// <param name="fortschritt">Optionale Fortschrittsmeldung in Prozent (0 bis 100).</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    Task<LogleseErgebnis> LesenAsync(
        Logquelle quelle,
        DateTime von,
        DateTime bis,
        IProgress<int>? fortschritt = null,
        CancellationToken abbruch = default);
}
