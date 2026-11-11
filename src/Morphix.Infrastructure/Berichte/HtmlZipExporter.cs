using System.IO.Compression;
using System.Text;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Berichte;

/// <summary>
/// Schreibt <c>bericht.html</c> und den Ordner <c>logs/</c> in eine ZIP-Datei. Die Logdateien werden nur
/// lesend geöffnet (GR05, <c>FileShare.ReadWrite</c>), die Originale bleiben unverändert.
/// </summary>
public sealed class HtmlZipExporter : IBerichtExporter
{
    private const string BerichtName = "bericht.html";
    private const string LogOrdner = "logs/";
    private const string HinweisName = LogOrdner + "_nicht_kopiert.txt";

    static HtmlZipExporter()
    {
        // Windows-1252 (ältere Logdateien mit Umlauten) steht erst nach Registrierung des Providers zur Verfügung.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <inheritdoc />
    public bool KannSchreiben(string zielpfad)
    {
        try
        {
            var ordner = Path.GetDirectoryName(Path.GetFullPath(zielpfad));
            if (string.IsNullOrEmpty(ordner) || !Directory.Exists(ordner))
            {
                return false;
            }

            // Probe mit einer Datei, die beim Schließen sofort verschwindet; es bleibt nichts im Ordner zurück.
            var probe = Path.Combine(ordner, $".morphix-{Guid.NewGuid():N}.tmp");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task ExportierenAsync(
        string html,
        IEnumerable<string> logdateien,
        string zielpfad,
        Func<string, string>? zeilenFilter = null,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(html);
        ArgumentNullException.ThrowIfNull(logdateien);

        try
        {
            await SchreibeZipAsync(html, logdateien, zielpfad, zeilenFilter, abbruch);
        }
        catch
        {
            // Eine halb geschriebene ZIP-Datei wäre irreführend (auch bei Abbruch).
            try
            {
                File.Delete(zielpfad);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Die ursprüngliche Ausnahme ist wichtiger als das fehlgeschlagene Aufräumen.
            }

            throw;
        }
    }

    private static async Task SchreibeZipAsync(
        string html, IEnumerable<string> logdateien, string zielpfad, Func<string, string>? zeilenFilter, CancellationToken abbruch)
    {
        await using var datei = new FileStream(zielpfad, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        using var zip = new ZipArchive(datei, ZipArchiveMode.Create);

        var bericht = zip.CreateEntry(BerichtName, CompressionLevel.Optimal);
        await using (var ziel = bericht.Open())
        await using (var schreiber = new StreamWriter(ziel, new UTF8Encoding(false)))
        {
            await schreiber.WriteAsync(html.AsMemory(), abbruch);
        }

        var vergebeneNamen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var nichtKopiert = new List<string>();
        foreach (var pfad in logdateien.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            abbruch.ThrowIfCancellationRequested();
            try
            {
                await KopiereLogAsync(zip, pfad, EindeutigerName(Path.GetFileName(pfad), vergebeneNamen), zeilenFilter, abbruch);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // NFA07: Eine unlesbare Datei bricht den Export nicht ab; sie wird im ZIP vermerkt.
                nichtKopiert.Add($"{Path.GetFileName(pfad)}: {ex.Message}");
            }
        }

        if (nichtKopiert.Count > 0)
        {
            var hinweis = zip.CreateEntry(HinweisName, CompressionLevel.Optimal);
            await using var ziel = hinweis.Open();
            await using var schreiber = new StreamWriter(ziel, new UTF8Encoding(false));
            await schreiber.WriteLineAsync("Diese Logdateien konnten nicht ins ZIP kopiert werden:".AsMemory(), abbruch);
            foreach (var zeile in nichtKopiert)
            {
                await schreiber.WriteLineAsync(zeile.AsMemory(), abbruch);
            }
        }
    }

    // Dateien aus verschiedenen Ordnern können gleich heißen; im ZIP bekommt jede einen eigenen Namen.
    private static string EindeutigerName(string name, HashSet<string> vergeben)
    {
        var kandidat = name;
        for (var nr = 2; !vergeben.Add(kandidat); nr++)
        {
            kandidat = $"{Path.GetFileNameWithoutExtension(name)}_{nr}{Path.GetExtension(name)}";
        }

        return kandidat;
    }

    private static async Task KopiereLogAsync(
        ZipArchive zip, string pfad, string name, Func<string, string>? zeilenFilter, CancellationToken abbruch)
    {
        await using var quelle = new FileStream(
            pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize: 81920, useAsync: true);

        // Der Eintrag entsteht erst, wenn die Datei geöffnet werden konnte; sonst bliebe ein leerer Eintrag zurück.
        var eintrag = zip.CreateEntry(LogOrdner + name, CompressionLevel.Optimal);
        eintrag.LastWriteTime = File.GetLastWriteTime(pfad);
        await using var ziel = eintrag.Open();

        if (zeilenFilter is null)
        {
            await quelle.CopyToAsync(ziel, abbruch);
            return;
        }

        var kodierung = IstGueltigesUtf8(quelle) ? new UTF8Encoding(false) : Encoding.GetEncoding(1252);
        quelle.Position = 0;
        using var leser = new StreamReader(quelle, kodierung, detectEncodingFromByteOrderMarks: true);
        await using var schreiber = new StreamWriter(ziel, new UTF8Encoding(false));
        while (await leser.ReadLineAsync(abbruch) is { } zeile)
        {
            await schreiber.WriteLineAsync(zeilenFilter(zeile).AsMemory(), abbruch);
        }
    }

    // Wie im DateiLogLeser: UTF-8, sonst Windows-1252. Die Prüfung liest die Datei einmal durch, ohne sie zu halten.
    private static bool IstGueltigesUtf8(Stream stream)
    {
        stream.Position = 0;
        try
        {
            using var leser = new StreamReader(
                stream, new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true, bufferSize: 81920, leaveOpen: true);
            var puffer = new char[81920];
            while (leser.Read(puffer, 0, puffer.Length) > 0)
            {
            }

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}
