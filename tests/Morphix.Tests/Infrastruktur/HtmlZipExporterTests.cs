using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Morphix.Infrastructure.Berichte;

namespace Morphix.Tests.Infrastruktur;

/// <summary>Tests des <see cref="HtmlZipExporter"/> mit temporären Dateien; die Originale dürfen sich nie ändern (GR05, NFA08).</summary>
public sealed class HtmlZipExporterTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "morphix-export-" + Guid.NewGuid().ToString("N"));
    private readonly HtmlZipExporter _exporter = new();

    public HtmlZipExporterTests()
    {
        Directory.CreateDirectory(_ordner);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_ordner, recursive: true);
        }
        catch (IOException)
        {
            // Aufräumen im Test; ein verbliebener Temp-Ordner ist unkritisch.
        }
    }

    private string Datei(string name, string inhalt, Encoding? kodierung = null)
    {
        var pfad = Path.Combine(_ordner, name);
        Directory.CreateDirectory(Path.GetDirectoryName(pfad)!);
        File.WriteAllText(pfad, inhalt, kodierung ?? new UTF8Encoding(false));
        return pfad;
    }

    private string Ziel(string name = "bericht.zip") => Path.Combine(_ordner, "ziel", name);

    private static string LiesEintrag(string zipPfad, string name, Encoding? kodierung = null)
    {
        using var zip = ZipFile.OpenRead(zipPfad);
        var eintrag = zip.GetEntry(name) ?? throw new InvalidOperationException($"Eintrag {name} fehlt.");
        using var leser = new StreamReader(eintrag.Open(), kodierung ?? Encoding.UTF8);
        return leser.ReadToEnd();
    }

    private static string[] Namen(string zipPfad)
    {
        using var zip = ZipFile.OpenRead(zipPfad);
        return [.. zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal)];
    }

    private async Task Exportiere(string html, IEnumerable<string> logs, string ziel, Func<string, string>? filter = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ziel)!);
        await _exporter.ExportierenAsync(html, logs, ziel, filter);
    }

    /// <summary>Das ZIP enthält bericht.html und die Logs im Ordner logs/.</summary>
    [Fact]
    public async Task Schreibt_Bericht_und_Logs()
    {
        var a = Datei("quelle/a.log", "2026-11-10 08:00:00 ERROR Fehler\n");
        var b = Datei("quelle/b.log", "zweite Datei\n");

        await Exportiere("<html>Bericht</html>", [a, b], Ziel());

        Assert.Equal(["bericht.html", "logs/a.log", "logs/b.log"], Namen(Ziel()));
        Assert.Equal("<html>Bericht</html>", LiesEintrag(Ziel(), "bericht.html"));
        Assert.Equal("2026-11-10 08:00:00 ERROR Fehler\n", LiesEintrag(Ziel(), "logs/a.log"));
    }

    /// <summary>Ohne Filter ist die Kopie byte-genau gleich (auch bei Windows-1252).</summary>
    [Fact]
    public async Task Kopie_ohne_Filter_ist_byte_genau()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Datei("ansi.log", "Größe überschritten\r\n", Encoding.GetEncoding(1252));
        var original = File.ReadAllBytes(ansi);

        await Exportiere("x", [ansi], Ziel());

        using var zip = ZipFile.OpenRead(Ziel());
        using var strom = zip.GetEntry("logs/ansi.log")!.Open();
        using var speicher = new MemoryStream();
        strom.CopyTo(speicher);
        Assert.Equal(original, speicher.ToArray());
    }

    /// <summary>GR05/NFA08: Die Originale behalten Inhalt und Änderungsdatum.</summary>
    [Fact]
    public async Task Originale_bleiben_unveraendert()
    {
        var a = Datei("a.log", "user=anna 10.1.2.3\nzweite Zeile\n");
        var zeit = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(a, zeit);
        var hashVorher = SHA256.HashData(File.ReadAllBytes(a));

        await Exportiere("x", [a], Ziel(), filter: z => z.Replace("anna", "[Benutzer]"));

        Assert.True(File.Exists(a));
        Assert.Equal(hashVorher, SHA256.HashData(File.ReadAllBytes(a)));
        Assert.Equal(zeit, File.GetLastWriteTimeUtc(a));
    }

    /// <summary>Mit Zeilenfilter (Maskierung) enthält das ZIP nur die gefilterten Zeilen, das Original nicht.</summary>
    [Fact]
    public async Task Zeilenfilter_wirkt_nur_auf_die_Kopie()
    {
        var a = Datei("a.log", "login user=anna\nIP 10.1.2.3\n");

        await Exportiere("x", [a], Ziel(), filter: z => z.Replace("anna", "[Benutzer]").Replace("10.1.2.3", "***"));

        var kopie = LiesEintrag(Ziel(), "logs/a.log");
        Assert.DoesNotContain("anna", kopie);
        Assert.DoesNotContain("10.1.2.3", kopie);
        Assert.Contains("[Benutzer]", kopie);
        Assert.Contains("anna", File.ReadAllText(a));
    }

    /// <summary>Auch Windows-1252-Dateien werden beim Filtern richtig gelesen (Umlaute bleiben erhalten, Ausgabe ist UTF-8).</summary>
    [Fact]
    public async Task Zeilenfilter_liest_Windows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var ansi = Datei("ansi.log", "Größe user=anna\n", Encoding.GetEncoding(1252));

        await Exportiere("x", [ansi], Ziel(), filter: z => z.Replace("anna", "[Benutzer]"));

        Assert.Equal("Größe user=[Benutzer]" + Environment.NewLine, LiesEintrag(Ziel(), "logs/ansi.log"));
    }

    /// <summary>Gleich heißende Dateien aus verschiedenen Ordnern überschreiben sich im ZIP nicht.</summary>
    [Fact]
    public async Task Gleiche_Dateinamen_werden_unterscheidbar()
    {
        var a1 = Datei("eins/app.log", "erste");
        var a2 = Datei("zwei/app.log", "zweite");

        await Exportiere("x", [a1, a2], Ziel());

        Assert.Equal(["bericht.html", "logs/app.log", "logs/app_2.log"], Namen(Ziel()));
        Assert.Equal("erste", LiesEintrag(Ziel(), "logs/app.log"));
        Assert.Equal("zweite", LiesEintrag(Ziel(), "logs/app_2.log"));
    }

    /// <summary>NFA07: Eine fehlende oder gesperrte Datei bricht den Export nicht ab, sie wird im ZIP vermerkt.</summary>
    [Fact]
    public async Task Nicht_lesbare_Datei_wird_vermerkt()
    {
        var gut = Datei("gut.log", "ok");
        var fehlt = Path.Combine(_ordner, "fehlt.log");
        var gesperrt = Datei("gesperrt.log", "geheim");

        await using (new FileStream(gesperrt, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Exportiere("x", [gut, fehlt, gesperrt], Ziel());
        }

        Assert.Equal(["bericht.html", "logs/_nicht_kopiert.txt", "logs/gut.log"], Namen(Ziel()));
        var hinweis = LiesEintrag(Ziel(), "logs/_nicht_kopiert.txt");
        Assert.Contains("fehlt.log", hinweis);
        Assert.Contains("gesperrt.log", hinweis);
    }

    /// <summary>Logs, in die gerade geschrieben wird (FileShare.ReadWrite), lassen sich trotzdem kopieren.</summary>
    [Fact]
    public async Task Offen_geschriebene_Datei_wird_kopiert()
    {
        var aktiv = Datei("aktiv.log", "laufend");

        await using (new FileStream(aktiv, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            await Exportiere("x", [aktiv], Ziel());
        }

        Assert.Equal("laufend", LiesEintrag(Ziel(), "logs/aktiv.log"));
    }

    /// <summary>Ohne Logdateien entsteht ein ZIP nur mit dem Bericht.</summary>
    [Fact]
    public async Task Ohne_Logs_nur_Bericht()
    {
        await Exportiere("x", [], Ziel());

        Assert.Equal(["bericht.html"], Namen(Ziel()));
    }

    /// <summary>Schlägt das Schreiben fehl, bleibt keine halbe ZIP-Datei zurück.</summary>
    [Fact]
    public async Task Abbruch_hinterlaesst_keine_Datei()
    {
        var a = Datei("a.log", "x");
        using var abbruch = new CancellationTokenSource();
        await abbruch.CancelAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(Ziel())!);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _exporter.ExportierenAsync("x", [a], Ziel(), null, abbruch.Token));

        Assert.False(File.Exists(Ziel()));
    }

    /// <summary>Ein nicht vorhandener Zielordner führt zu einer IOException; die ZIP-Datei entsteht nicht.</summary>
    [Fact]
    public async Task Fehlender_Zielordner_wirft_IOException()
    {
        var ziel = Path.Combine(_ordner, "gibt-es-nicht", "x.zip");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => _exporter.ExportierenAsync("x", [], ziel));
    }

    /// <summary>„Exportordner“: Ein vorhandener, beschreibbarer Ordner ist gültig und bleibt unverändert (keine Probedatei).</summary>
    [Fact]
    public void KannSchreiben_in_vorhandenen_Ordner()
    {
        Assert.True(_exporter.KannSchreiben(Path.Combine(_ordner, "x.zip")));
        Assert.Empty(Directory.GetFileSystemEntries(_ordner));
    }

    /// <summary>„Exportordner“: Ein nicht vorhandener Ordner oder ein ungültiger Pfad ist nicht beschreibbar.</summary>
    [Theory]
    [InlineData("gibt-es-nicht/x.zip")]
    [InlineData("")]
    public void KannSchreiben_false_bei_ungueltigem_Ziel(string relativ)
    {
        var pfad = relativ.Length == 0 ? "" : Path.Combine(_ordner, relativ);

        Assert.False(_exporter.KannSchreiben(pfad));
    }
}
