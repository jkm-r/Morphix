using System.Text;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Demo;

/// <summary>
/// Liest die mitgelieferten Demo-Logdateien. Alle Zeitstempel werden so verschoben, dass der jüngste
/// Eintrag der Quelle „jetzt“ ist; der Standardzeitraum liefert dadurch immer Treffer.
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
        var parser = new LogParser(quelle.Zeilenmuster);
        var dateien = Directory.Exists(ordner)
            ? Directory.EnumerateFiles(ordner, quelle.Dateimuster).Order(StringComparer.OrdinalIgnoreCase).ToList()
            : [];

        var alle = new List<Logeintrag>();
        var uebersprungen = new List<UebersprungeneDatei>();
        long bytes = 0;
        var gelesen = 0;

        for (var i = 0; i < dateien.Count; i++)
        {
            abbruch.ThrowIfCancellationRequested();
            try
            {
                // GR05: nur lesend öffnen, auch bei Demo-Dateien.
                await using var stream = new FileStream(dateien[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var zeilen = new List<string>();
                while (await reader.ReadLineAsync(abbruch) is { } zeile)
                {
                    zeilen.Add(zeile);
                }

                alle.AddRange(parser.Parse(zeilen, Path.GetFileName(dateien[i])));
                bytes += stream.Length;
                gelesen++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                uebersprungen.Add(new UebersprungeneDatei(dateien[i], "Die Datei ist nicht lesbar oder gesperrt."));
            }

            fortschritt?.Report((i + 1) * 100 / dateien.Count);
        }

        if (alle.Count == 0)
        {
            return new LogleseErgebnis([], gelesen, bytes, uebersprungen);
        }

        var verschiebung = _jetzt() - alle.Max(e => e.Zeitstempel);
        var imZeitraum = alle
            .Select(e => e with { Zeitstempel = e.Zeitstempel + verschiebung })
            .Where(e => e.Zeitstempel >= von && e.Zeitstempel <= bis)
            .OrderBy(e => e.Zeitstempel)
            .ToList();

        return new LogleseErgebnis(imZeitraum, gelesen, bytes, uebersprungen);
    }
}
