using System.Security.Cryptography;
using System.Text;
using Morphix.Core.Domaene;
using Morphix.Infrastructure;
using Morphix.Infrastructure.Dateien;
using Morphix.Infrastructure.Leer;

namespace Morphix.Tests.Infrastruktur;

/// <summary>Tests des <see cref="DateiLogLeser"/> mit temporären Dateien (GR05, NFA07, NFA08).</summary>
public sealed class DateiLogLeserTests : IDisposable
{
    private static readonly DateTime Von = new(2026, 11, 1, 0, 0, 0);
    private static readonly DateTime Bis = new(2026, 11, 7, 23, 59, 59);

    private readonly string _ordner = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "morphix-log-" + Guid.NewGuid().ToString("N"));

    /// <summary>Legt den temporären Ordner an.</summary>
    public DateiLogLeserTests()
    {
        Directory.CreateDirectory(_ordner);
    }

    /// <summary>Räumt den temporären Ordner auf.</summary>
    public void Dispose()
    {
        Directory.Delete(_ordner, recursive: true);
    }

    private Logquelle Quelle(string muster = "*.log", string? zeilenmuster = null) =>
        new() { Pfad = _ordner, Dateimuster = muster, Zeilenmuster = zeilenmuster };

    private string Datei(string name, string inhalt, DateTime? geaendert = null, Encoding? kodierung = null)
    {
        var pfad = System.IO.Path.Combine(_ordner, name);
        File.WriteAllText(pfad, inhalt, kodierung ?? new UTF8Encoding(false));
        File.SetLastWriteTime(pfad, geaendert ?? Von.AddDays(2));
        return pfad;
    }

    private static Task<LogleseErgebnis> Lese(DateiLogLeser leser, Logquelle quelle, IProgress<int>? p = null) =>
        leser.LesenAsync(quelle, Von, Bis, p);

    /// <summary>Einträge im Zeitraum werden gelesen, andere verworfen; das Ergebnis ist nach Zeit sortiert.</summary>
    [Fact]
    public async Task Liest_Eintraege_im_Zeitraum()
    {
        Datei("a.log",
            "2026-10-31 23:59:59 INFO davor\n" +
            "2026-11-03 10:00:00 ERROR mitten\n" +
            "2026-11-02 09:00:00 INFO fruehere\n" +
            "2026-11-08 00:00:00 INFO danach\n");

        var ergebnis = await Lese(new DateiLogLeser(), Quelle());

        Assert.Equal(["fruehere", "mitten"], ergebnis.Eintraege.Select(e => e.Meldung));
        Assert.Equal(1, ergebnis.Dateianzahl);
        Assert.True(ergebnis.Bytes > 0);
        Assert.Empty(ergebnis.Uebersprungen);
        Assert.Equal("a.log", ergebnis.Eintraege[0].Quelle);
    }

    /// <summary>Nur Dateien passend zum Dateimuster und mit Änderungsdatum nicht vor dem Zeitraum werden gelesen.</summary>
    [Fact]
    public async Task Waehlt_Dateien_nach_Muster_und_Aenderungsdatum()
    {
        const string zeile = "2026-11-03 10:00:00 INFO x\n";
        var passend = Datei("neu.log", zeile);
        Datei("alt.log", zeile, geaendert: Von.AddDays(-3));
        Datei("andere.txt", zeile);
        Datei("noch-neuer.log", zeile, geaendert: Bis.AddDays(10)); // später weitergeschrieben: kann alte Einträge enthalten

        var ergebnis = await Lese(new DateiLogLeser(), Quelle());

        Assert.Equal(
            [System.IO.Path.Combine(_ordner, "neu.log"), System.IO.Path.Combine(_ordner, "noch-neuer.log")],
            ergebnis.GeleseneDateien.Order(StringComparer.OrdinalIgnoreCase));
        Assert.Contains(passend, ergebnis.GeleseneDateien);
    }

    /// <summary>Mehrzeilige Meldungen (GR06) funktionieren auch über die Datei.</summary>
    [Fact]
    public async Task Mehrzeilige_Meldung_aus_Datei()
    {
        Datei("a.log", "2026-11-03 10:00:00 ERROR Fehler\n   bei X.Y()\n   bei Z()\n");

        var e = Assert.Single((await Lese(new DateiLogLeser(), Quelle())).Eintraege);

        Assert.Equal("Fehler\n   bei X.Y()\n   bei Z()", e.Meldung);
    }

    /// <summary>Umlaute in UTF-8 werden korrekt gelesen, auch mit BOM.</summary>
    [Fact]
    public async Task Liest_UTF8_mit_und_ohne_BOM()
    {
        Datei("ohne.log", "2026-11-03 10:00:00 INFO Größe äöü\n");
        Datei("mit.log", "2026-11-03 10:00:01 INFO Größe äöü\n", kodierung: new UTF8Encoding(true));

        var ergebnis = await Lese(new DateiLogLeser(), Quelle());

        Assert.Equal(2, ergebnis.Eintraege.Count);
        Assert.All(ergebnis.Eintraege, e => Assert.Equal("Größe äöü", e.Meldung));
    }

    /// <summary>Eine Datei in Windows-1252 (ungültiges UTF-8) wird per Fallback korrekt gelesen.</summary>
    [Fact]
    public async Task Faellt_auf_Windows_1252_zurueck()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Datei("ansi.log", "2026-11-03 10:00:00 INFO Größe äöü ß\n", kodierung: Encoding.GetEncoding(1252));

        var e = Assert.Single((await Lese(new DateiLogLeser(), Quelle())).Eintraege);

        Assert.Equal("Größe äöü ß", e.Meldung);
    }

    /// <summary>NFA07: Eine leere Datei ist kein Fehler.</summary>
    [Fact]
    public async Task Leere_Datei()
    {
        Datei("leer.log", "");

        var ergebnis = await Lese(new DateiLogLeser(), Quelle());

        Assert.Empty(ergebnis.Eintraege);
        Assert.Equal(1, ergebnis.Dateianzahl);
        Assert.Empty(ergebnis.Uebersprungen);
    }

    /// <summary>NFA07: Eine Binärdatei und eine Datei ohne Zeitstempel ergeben keine Einträge und keinen Absturz.</summary>
    [Fact]
    public async Task Binaer_und_ohne_Zeitstempel()
    {
        var binaer = System.IO.Path.Combine(_ordner, "binaer.log");
        File.WriteAllBytes(binaer, [0x00, 0xFF, 0xFE, 0x80, 0x81, 0x00, 0x0D, 0xC3, 0x28]);
        File.SetLastWriteTime(binaer, Von.AddDays(1));
        Datei("text.log", "nur Text\nohne Zeitstempel\n");

        var ergebnis = await Lese(new DateiLogLeser(), Quelle());

        Assert.Empty(ergebnis.Eintraege);
        Assert.Empty(ergebnis.Uebersprungen);
    }

    /// <summary>NFA07: Eine exklusiv gesperrte Datei wird übersprungen und gemeldet; die anderen werden gelesen.</summary>
    [Fact]
    public async Task Gesperrte_Datei_wird_uebersprungen()
    {
        Datei("gut.log", "2026-11-03 10:00:00 INFO ok\n");
        var gesperrt = Datei("gesperrt.log", "2026-11-03 11:00:00 INFO gesperrt\n");
        using var sperre = new FileStream(gesperrt, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var ergebnis = await Lese(new DateiLogLeser(), Quelle());

        Assert.Equal(["ok"], ergebnis.Eintraege.Select(e => e.Meldung));
        var u = Assert.Single(ergebnis.Uebersprungen);
        Assert.Equal(gesperrt, u.Pfad);
        Assert.Contains("gesperrt", u.Grund);
        Assert.Equal(1, ergebnis.Dateianzahl);
    }

    /// <summary>GR05: Eine Datei, in die gerade geschrieben wird (FileShare.ReadWrite), kann gelesen werden.</summary>
    [Fact]
    public async Task Liest_Datei_die_gerade_beschrieben_wird()
    {
        var pfad = Datei("aktiv.log", "2026-11-03 10:00:00 INFO laufend\n");
        await using var schreiber = new FileStream(pfad, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

        var ergebnis = await Lese(new DateiLogLeser(), Quelle());

        Assert.Single(ergebnis.Eintraege);
        Assert.Empty(ergebnis.Uebersprungen);
    }

    /// <summary>NFA07: Ein fehlender Ordner wird gemeldet statt eine Ausnahme zu werfen.</summary>
    [Fact]
    public async Task Fehlender_Ordner_wird_gemeldet()
    {
        var quelle = Quelle() with { Pfad = System.IO.Path.Combine(_ordner, "gibt-es-nicht") };

        var ergebnis = await Lese(new DateiLogLeser(), quelle);

        Assert.Empty(ergebnis.Eintraege);
        var u = Assert.Single(ergebnis.Uebersprungen);
        Assert.Contains("Ordner", u.Grund);
    }

    /// <summary>Ein eigenes Zeilenmuster der Logquelle wird verwendet.</summary>
    [Fact]
    public async Task Eigenes_Zeilenmuster()
    {
        Datei("x.log", "2026-11-03 10:00:00 | CRIT | Platte voll\n");
        var quelle = Quelle(zeilenmuster: @"^(?<zeit>\S+ \S+) \| (?<stufe>\w+) \| (?<text>.*)$");

        var e = Assert.Single((await Lese(new DateiLogLeser(), quelle)).Eintraege);

        Assert.Equal(Schweregrad.Kritisch, e.Schweregrad);
    }

    /// <summary>Der Fortschritt erreicht 100 Prozent.</summary>
    [Fact]
    public async Task Meldet_Fortschritt()
    {
        Datei("a.log", "2026-11-03 10:00:00 INFO a\n");
        Datei("b.log", "2026-11-03 10:00:00 INFO b\n");
        var werte = new List<int>();

        await Lese(new DateiLogLeser(), Quelle(), new SofortFortschritt(werte));

        Assert.Equal([50, 100], werte);
    }

    /// <summary>NFA08: Nach der Analyse sind Inhalt (SHA-256) und Änderungsdatum der Dateien unverändert.</summary>
    [Fact]
    public async Task Originaldateien_bleiben_unveraendert()
    {
        var pfade = new[]
        {
            Datei("a.log", "2026-11-03 10:00:00 ERROR a\n   bei X()\n", geaendert: new DateTime(2026, 11, 4, 8, 0, 0)),
            Datei("b.log", "2026-11-04 10:00:00 INFO b\n", geaendert: new DateTime(2026, 11, 5, 9, 30, 0)),
        };
        var vorher = pfade.Select(p => (Hash: Hash(p), Zeit: File.GetLastWriteTimeUtc(p))).ToList();

        await Lese(new DateiLogLeser(), Quelle());

        var nachher = pfade.Select(p => (Hash: Hash(p), Zeit: File.GetLastWriteTimeUtc(p))).ToList();
        Assert.Equal(vorher, nachher);
        Assert.Equal(2, Directory.GetFiles(_ordner).Length);
    }

    /// <summary>Die Fabrik liefert für echte Profile den Datei-Leser und leere Platzhalter statt einer Ausnahme.</summary>
    [Fact]
    public async Task Fabrik_liefert_fuer_echte_Profile_DateiLogLeser_Dienst_Adapter_und_leere_Platzhalter()
    {
        // Fakes statt echter Dienste: Tests steuern nie ein echtes System.
        var dienste = NSubstitute.Substitute.For<Morphix.Core.Schnittstellen.IDienstGateway>();
        var rechte = NSubstitute.Substitute.For<Morphix.Core.Schnittstellen.IRechtePruefer>();
        var fabrik = new WindowsDatenquellenFabrik("demo", dienste, rechte);

        var quellen = fabrik.Erzeugen(new Systemprofil { Name = "Echt", IstDemo = false });

        Assert.IsType<DateiLogLeser>(quellen.LogLeser);
        Assert.Same(dienste, quellen.Dienste);
        Assert.Same(rechte, quellen.Rechte);
        Assert.Empty(await quellen.Aufgaben.AbfragenAsync([new GeplanteAufgabe { Name = "x" }]));
        Assert.Empty(await quellen.Ereignisse.LesenAsync(new Logquelle { Typ = LogquellenTyp.Ereignisanzeige, Pfad = "Application" }, Von, Bis));
    }

    private static string Hash(string pfad) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pfad)));

    // Progress<T> meldet asynchron; für exakte Reihenfolge in Tests wird synchron gesammelt.
    private sealed class SofortFortschritt(List<int> werte) : IProgress<int>
    {
        public void Report(int value) => werte.Add(value);
    }
}
