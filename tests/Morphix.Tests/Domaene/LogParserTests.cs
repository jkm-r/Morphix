using Morphix.Core.Domaene;

namespace Morphix.Tests.Domaene;

/// <summary>Tests für den <see cref="LogParser"/> (inkl. GR06).</summary>
public class LogParserTests
{
    private static List<Logeintrag> Parse(params string[] zeilen) =>
        [.. new LogParser().Parse(zeilen, "test.log")];

    /// <summary>Eine Standardzeile wird in Zeit, Stufe und Text zerlegt.</summary>
    [Fact]
    public void Standardzeile()
    {
        var e = Assert.Single(Parse("2026-11-11 10:15:30 [ERROR] Datenbank nicht erreichbar"));

        Assert.Equal(new DateTime(2026, 11, 11, 10, 15, 30), e.Zeitstempel);
        Assert.Equal(Schweregrad.Fehler, e.Schweregrad);
        Assert.Equal("test.log", e.Quelle);
        Assert.Equal("Datenbank nicht erreichbar", e.Meldung);
    }

    /// <summary>Stufe ohne Klammern, ISO-Trennzeichen T und Millisekunden mit Komma oder Punkt.</summary>
    [Theory]
    [InlineData("2026-11-11T10:15:30.123 WARN Text", 123)]
    [InlineData("2026-11-11 10:15:30,456 WARN Text", 456)]
    public void Zeitformate(string zeile, int ms)
    {
        var e = Assert.Single(Parse(zeile));

        Assert.Equal(new DateTime(2026, 11, 11, 10, 15, 30, ms), e.Zeitstempel);
        Assert.Equal(Schweregrad.Warnung, e.Schweregrad);
    }

    /// <summary>Das Stufen-Mapping aus dem Umsetzungsplan, ohne Beachtung der Schreibweise.</summary>
    [Theory]
    [InlineData("TRACE", Schweregrad.Info)]
    [InlineData("debug", Schweregrad.Info)]
    [InlineData("Info", Schweregrad.Info)]
    [InlineData("WARN", Schweregrad.Warnung)]
    [InlineData("warning", Schweregrad.Warnung)]
    [InlineData("ERR", Schweregrad.Fehler)]
    [InlineData("Error", Schweregrad.Fehler)]
    [InlineData("FATAL", Schweregrad.Kritisch)]
    [InlineData("CRIT", Schweregrad.Kritisch)]
    [InlineData("critical", Schweregrad.Kritisch)]
    [InlineData("SEHRLAUT", Schweregrad.Info)]
    [InlineData("", Schweregrad.Info)]
    [InlineData(null, Schweregrad.Info)]
    public void Stufenzuordnung(string? stufe, Schweregrad erwartet)
    {
        Assert.Equal(erwartet, LogParser.ZuSchweregrad(stufe));
    }

    /// <summary>Eine unbekannte Stufe in einer Zeile ergibt Info, die Zeile bleibt ein eigener Eintrag.</summary>
    [Fact]
    public void Unbekannte_Stufe_ergibt_Info()
    {
        var e = Assert.Single(Parse("2026-11-11 10:00:00 [VERBOSE] Text"));

        Assert.Equal(Schweregrad.Info, e.Schweregrad);
    }

    /// <summary>GR06: Zeilen ohne Zeitstempel hängen am vorherigen Eintrag.</summary>
    [Fact]
    public void Mehrzeilige_Meldung()
    {
        var eintraege = Parse(
            "2026-11-11 10:00:00 [ERROR] Export fehlgeschlagen",
            "   at Export.Run() in C:\\src\\Export.cs:line 42",
            "   at Program.Main()",
            "2026-11-11 10:00:01 [INFO] Weiter");

        Assert.Equal(2, eintraege.Count);
        Assert.Equal(
            "Export fehlgeschlagen\n   at Export.Run() in C:\\src\\Export.cs:line 42\n   at Program.Main()",
            eintraege[0].Meldung);
        Assert.Equal("Weiter", eintraege[1].Meldung);
    }

    /// <summary>GR06: Zeilen vor dem ersten Eintrag werden verworfen.</summary>
    [Fact]
    public void Zeilen_vor_dem_ersten_Eintrag_werden_verworfen()
    {
        var eintraege = Parse("Kopfzeile", "noch eine", "2026-11-11 10:00:00 INFO Start");

        Assert.Single(eintraege);
        Assert.Equal("Start", eintraege[0].Meldung);
    }

    /// <summary>Leere Eingabe und Eingabe ohne jeden Zeitstempel ergeben keine Einträge.</summary>
    [Fact]
    public void Leere_Eingabe()
    {
        Assert.Empty(Parse());
        Assert.Empty(Parse("nur Text", "und noch mehr"));
    }

    /// <summary>Ein unmöglicher Zeitstempel zählt als Zeile ohne Zeitstempel.</summary>
    [Fact]
    public void Unmoeglicher_Zeitstempel_ist_Fortsetzungszeile()
    {
        var eintraege = Parse("2026-11-11 10:00:00 INFO Start", "2026-13-45 10:00:00 INFO kaputt");

        var e = Assert.Single(eintraege);
        Assert.Contains("kaputt", e.Meldung);
    }

    /// <summary>Ein eigenes Zeilenmuster mit den Gruppen zeit, stufe und text wird verwendet.</summary>
    [Fact]
    public void Eigenes_Zeilenmuster()
    {
        var parser = new LogParser(@"^(?<zeit>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}) \| (?<stufe>\w+) \| (?<text>.*)$");

        var e = Assert.Single(parser.Parse(["2026-11-11 10:00:00 | CRIT | Lizenz abgelaufen"], "x"));

        Assert.Equal(Schweregrad.Kritisch, e.Schweregrad);
        Assert.Equal("Lizenz abgelaufen", e.Meldung);
    }

    /// <summary>Ungültige Muster oder Muster ohne die drei Gruppen werden abgelehnt.</summary>
    [Theory]
    [InlineData("(")]
    [InlineData(@"^(?<zeit>\d+) (?<text>.*)$")]
    public void Ungueltiges_Muster_wirft(string muster)
    {
        Assert.Throws<ArgumentException>(() => new LogParser(muster));
    }

    /// <summary>Ein leeres Muster fällt auf das Standardmuster zurück.</summary>
    [Fact]
    public void Leeres_Muster_nutzt_Standard()
    {
        var parser = new LogParser("  ");

        Assert.Single(parser.Parse(["2026-11-11 10:00:00 INFO a"], "x"));
    }
}
