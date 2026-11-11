using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure.Dateien;

namespace Morphix.Infrastructure.Demo;

/// <summary>
/// Liest die mitgelieferten Demo-Logdateien. Alle Zeitstempel werden so verschoben, dass der jüngste
/// Eintrag der Quelle „jetzt“ ist; der Standardzeitraum liefert dadurch immer Treffer.
/// Das Einlesen selbst übernimmt der <see cref="DateiLogLeser"/>.
/// </summary>
public sealed class DemoLogLeser : ILogLeser
{
    private readonly string _demoOrdner;
    private readonly Func<DateTime> _jetzt;

    /// <summary>Erzeugt den Leser.</summary>
    /// <param name="demoOrdner">Ordner <c>demo</c>; relative Pfade der Logquelle gelten ab hier.</param>
    /// <param name="jetzt">Uhr; Tests übergeben eine feste Zeit.</param>
    public DemoLogLeser(string demoOrdner, Func<DateTime>? jetzt = null)
    {
        _demoOrdner = demoOrdner;
        _jetzt = jetzt ?? (() => DateTime.Now);
    }

    /// <inheritdoc />
    public async Task<LogleseErgebnis> LesenAsync(
        Logquelle quelle, DateTime von, DateTime bis, IProgress<int>? fortschritt = null, CancellationToken abbruch = default)
    {
        var ordner = Path.IsPathRooted(quelle.Pfad) ? quelle.Pfad : Path.Combine(_demoOrdner, quelle.Pfad);

        // Das Änderungsdatum der Demo-Dateien ist bedeutungslos; alle Dateien werden gelesen.
        var roh = await DateiLogLeser.LeseDateienAsync(quelle with { Pfad = ordner }, _ => true, fortschritt, abbruch);
        if (roh.Eintraege.Count == 0)
        {
            return roh;
        }

        var verschiebung = _jetzt() - roh.Eintraege.Max(e => e.Zeitstempel);
        var imZeitraum = roh.Eintraege
            .Select(e => e with { Zeitstempel = e.Zeitstempel + verschiebung })
            .Where(e => e.Zeitstempel >= von && e.Zeitstempel <= bis)
            .OrderBy(e => e.Zeitstempel)
            .ToList();

        return roh with { Eintraege = imZeitraum };
    }
}
