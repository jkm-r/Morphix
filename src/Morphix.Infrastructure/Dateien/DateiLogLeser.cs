using System.Text;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Dateien;

/// <summary>
/// Liest Logdateien aus einem Ordner (P2). Dateien werden ausschließlich lesend und mit
/// <c>FileShare.ReadWrite</c> geöffnet (GR05, NFA08), damit auch Logs gelesen werden können, in die das
/// System gerade schreibt. Unlesbare oder gesperrte Dateien werden übersprungen und gemeldet (NFA07).
/// </summary>
public sealed class DateiLogLeser : ILogLeser
{
    static DateiLogLeser()
    {
        // Windows-1252 ist in .NET nur nach Registrierung des CodePages-Providers verfügbar (Teil des Frameworks).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <inheritdoc />
    public async Task<LogleseErgebnis> LesenAsync(
        Logquelle quelle, DateTime von, DateTime bis, IProgress<int>? fortschritt = null, CancellationToken abbruch = default)
    {
        // Eine Datei kann Einträge im Zeitraum enthalten, wenn sie nach Beginn des Zeitraums noch geändert wurde.
        // Das Änderungsdatum nach dem Ende ist unkritisch: die Datei kann trotzdem ältere Einträge enthalten
        // (z. B. eine Logdatei, in die seitdem weitergeschrieben wurde).
        var roh = await LeseDateienAsync(quelle, d => d.LastWriteTime >= von, fortschritt, abbruch);

        var imZeitraum = roh.Eintraege
            .Where(e => e.Zeitstempel >= von && e.Zeitstempel <= bis)
            .OrderBy(e => e.Zeitstempel)
            .ToList();
        return roh with { Eintraege = imZeitraum };
    }

    /// <summary>
    /// Liest alle zur Quelle passenden Dateien <b>ohne</b> Zeitraumfilter auf den Einträgen. Wird auch vom
    /// Demo-Leser verwendet, der die Zeitstempel danach verschiebt.
    /// </summary>
    /// <param name="quelle">Ordner, Dateimuster und optionales Zeilenmuster.</param>
    /// <param name="dateiFilter">Auswahl der Dateien anhand ihrer Eigenschaften (z. B. Änderungsdatum).</param>
    /// <param name="fortschritt">Fortschritt in Prozent.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    internal static async Task<LogleseErgebnis> LeseDateienAsync(
        Logquelle quelle, Func<FileInfo, bool> dateiFilter, IProgress<int>? fortschritt, CancellationToken abbruch)
    {
        var uebersprungen = new List<UebersprungeneDatei>();
        var parser = new LogParser(quelle.Zeilenmuster);

        List<FileInfo> dateien;
        try
        {
            dateien = [.. new DirectoryInfo(quelle.Pfad).EnumerateFiles(quelle.Dateimuster)
                .Where(dateiFilter)
                .OrderBy(d => d.FullName, StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // Fehlender oder nicht lesbarer Ordner, ungültiger Pfad: melden statt abstürzen.
            fortschritt?.Report(100);
            return new LogleseErgebnis([], 0, 0, [new UebersprungeneDatei(quelle.Pfad, "Der Ordner existiert nicht oder ist nicht lesbar.")], []);
        }

        var eintraege = new List<Logeintrag>();
        var gelesen = new List<string>();
        long bytes = 0;

        for (var i = 0; i < dateien.Count; i++)
        {
            abbruch.ThrowIfCancellationRequested();
            var datei = dateien[i];
            try
            {
                var neu = await LeseDateiAsync(datei.FullName, parser, abbruch);
                eintraege.AddRange(neu);
                gelesen.Add(datei.FullName);
                bytes += datei.Length;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                uebersprungen.Add(new UebersprungeneDatei(datei.FullName, "Die Datei ist nicht lesbar oder gesperrt."));
            }

            fortschritt?.Report((i + 1) * 100 / dateien.Count);
        }

        if (dateien.Count == 0)
        {
            fortschritt?.Report(100);
        }

        return new LogleseErgebnis(eintraege, gelesen.Count, bytes, uebersprungen, gelesen);
    }

    // UTF-8 mit Fallback auf Windows-1252: Enthält die Datei ungültige UTF-8-Folgen (typisch für ANSI-Dateien
    // mit Umlauten), wird sie komplett neu mit Windows-1252 gelesen.
    private static async Task<List<Logeintrag>> LeseDateiAsync(string pfad, LogParser parser, CancellationToken abbruch)
    {
        var quellname = Path.GetFileName(pfad);
        try
        {
            return await LeseMitAsync(pfad, quellname, parser, new UTF8Encoding(false, throwOnInvalidBytes: true), abbruch);
        }
        catch (DecoderFallbackException)
        {
            return await LeseMitAsync(pfad, quellname, parser, Encoding.GetEncoding(1252), abbruch);
        }
    }

    private static async Task<List<Logeintrag>> LeseMitAsync(
        string pfad, string quellname, LogParser parser, Encoding kodierung, CancellationToken abbruch)
    {
        // GR05: nur lesen, andere dürfen weiter schreiben.
        await using var stream = new FileStream(
            pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 81920, useAsync: true);
        using var reader = new StreamReader(stream, kodierung, detectEncodingFromByteOrderMarks: true);

        var zeilen = new List<string>();
        while (await reader.ReadLineAsync(abbruch) is { } zeile)
        {
            zeilen.Add(zeile);
        }

        return [.. parser.Parse(zeilen, quellname)];
    }
}
