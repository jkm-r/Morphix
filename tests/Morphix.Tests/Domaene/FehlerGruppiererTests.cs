using Morphix.Core.Domaene;

namespace Morphix.Tests.Domaene;

/// <summary>Tests für den <see cref="FehlerGruppierer"/> (P3, GR07).</summary>
public class FehlerGruppiererTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 8, 0, 0);

    private static Logeintrag Eintrag(string meldung, int minuten = 0, Schweregrad grad = Schweregrad.Fehler, string quelle = "a.log") =>
        new(T0.AddMinutes(minuten), grad, quelle, meldung);

    /// <summary>Beispiel aus der Spezifikation.</summary>
    [Fact]
    public void Spezifikationsbeispiel()
    {
        Assert.Equal("Timeout nach # s bei Terminal #", FehlerGruppierer.Normalisiere("Timeout nach 30 s bei Terminal 12"));
    }

    /// <summary>Jede Normalisierungsart einzeln.</summary>
    [Theory]
    [InlineData("Sitzung 3f2504e0-4f89-11d3-9a0c-0305e82c3301 beendet", "Sitzung # beendet")]
    [InlineData("Start 2026-11-11 10:15:30.123 fehlgeschlagen", "Start # fehlgeschlagen")]
    [InlineData("Start 2026-11-11T10:15:30 fehlgeschlagen", "Start # fehlgeschlagen")]
    [InlineData("Am 11.11.2026 14:30 fehlgeschlagen", "Am # fehlgeschlagen")]
    [InlineData("Datei C:\\Daten\\Export\\export_2026.csv fehlt", "Datei # fehlt")]
    [InlineData("Datei C:\\Daten\\Export\\export.csv, danach Abbruch", "Datei #, danach Abbruch")]
    [InlineData("Freigabe \\\\server\\share\\x.dat fehlt", "Freigabe # fehlt")]
    [InlineData("Verbindung zu 192.168.10.25 verloren", "Verbindung zu # verloren")]
    [InlineData("Verbindung zu 10.0.0.1:8080 verloren", "Verbindung zu #:# verloren")]
    [InlineData("Export-Job beendet mit Code 0x1", "Export-Job beendet mit Code #")]
    [InlineData("Fehler 0x80070005 beim Zugriff", "Fehler # beim Zugriff")]
    [InlineData("Wert 3,5 und 42", "Wert # und #")]
    [InlineData("Nichts Variables hier", "Nichts Variables hier")]
    [InlineData("", "")]
    public void Normalisierung(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, FehlerGruppierer.Normalisiere(eingabe));
    }

    /// <summary>GR07: Info und Warnung bilden keine Fehlergruppen.</summary>
    [Fact]
    public void Nur_Fehler_und_Kritisch()
    {
        var gruppen = FehlerGruppierer.Gruppiere(
        [
            Eintrag("a 1", grad: Schweregrad.Info),
            Eintrag("a 2", grad: Schweregrad.Warnung),
            Eintrag("b 3", grad: Schweregrad.Fehler),
            Eintrag("c 4", grad: Schweregrad.Kritisch),
        ]);

        Assert.Equal(2, gruppen.Count);
        Assert.DoesNotContain(gruppen, g => g.Meldungsmuster.StartsWith('a'));
    }

    /// <summary>Anzahl, erstes und letztes Auftreten; absteigend nach Anzahl sortiert.</summary>
    [Fact]
    public void Gruppen_zaehlen_und_sortieren()
    {
        var gruppen = FehlerGruppierer.Gruppiere(
        [
            Eintrag("Datenbank verloren", 5),
            Eintrag("Timeout nach 30 s bei Terminal 12", 30),
            Eintrag("Timeout nach 45 s bei Terminal 7", 10),
            Eintrag("Timeout nach 31 s bei Terminal 99", 20),
        ]);

        Assert.Equal(2, gruppen.Count);
        var erste = gruppen[0];
        Assert.Equal("Timeout nach # s bei Terminal #", erste.Meldungsmuster);
        Assert.Equal(3, erste.Anzahl);
        Assert.Equal(T0.AddMinutes(10), erste.ErstesAuftreten);
        Assert.Equal(T0.AddMinutes(30), erste.LetztesAuftreten);
        Assert.Equal(3, erste.Eintraege.Count);
        Assert.Equal(1, gruppen[1].Anzahl);
    }

    /// <summary>Gleiche Muster aus verschiedenen Quellen bleiben getrennte Gruppen.</summary>
    [Fact]
    public void Quelle_gehoert_zum_Schluessel()
    {
        var gruppen = FehlerGruppierer.Gruppiere(
        [
            Eintrag("Fehler 1", quelle: "a.log"),
            Eintrag("Fehler 2", quelle: "b.log"),
        ]);

        Assert.Equal(2, gruppen.Count);
    }

    /// <summary>Bei gleicher Anzahl entscheidet das jüngere Auftreten; die Reihenfolge ist deterministisch.</summary>
    [Fact]
    public void Gleichstand_ist_deterministisch()
    {
        var gruppen = FehlerGruppierer.Gruppiere([Eintrag("alt", 1), Eintrag("neu", 9)]);

        Assert.Equal(["neu", "alt"], gruppen.Select(g => g.Meldungsmuster));
    }

    /// <summary>Leere Eingabe ergibt keine Gruppen.</summary>
    [Fact]
    public void Leere_Eingabe()
    {
        Assert.Empty(FehlerGruppierer.Gruppiere([]));
    }

    /// <summary>
    /// Zwei gleiche Fehler mit unterschiedlichem Stacktrace landen in einer Gruppe; das Muster stammt nur
    /// aus der ersten Zeile, der vollständige Text bleibt im Logeintrag erhalten.
    /// </summary>
    [Fact]
    public void Unterschiedlicher_Stacktrace_gehoert_zur_selben_Gruppe()
    {
        var a = Eintrag("Export fehlgeschlagen\n   at Export.Run() in C:\\src\\Export.cs:line 42", 1);
        var b = Eintrag("Export fehlgeschlagen\n   at Export.Schreibe() in C:\\src\\Writer.cs:line 7\n   at Program.Main()", 2);

        var gruppen = FehlerGruppierer.Gruppiere([a, b]);

        var g = Assert.Single(gruppen);
        Assert.Equal("Export fehlgeschlagen", g.Meldungsmuster);
        Assert.Equal(2, g.Anzahl);
        Assert.Equal([a, b], g.Eintraege);
        Assert.Contains("Writer.cs", g.Eintraege[1].Meldung);
    }

    /// <summary>Unterschiedliche erste Zeilen bleiben getrennt, auch bei gleichem Stacktrace; CRLF stört nicht.</summary>
    [Fact]
    public void Erste_Zeile_entscheidet()
    {
        var gruppen = FehlerGruppierer.Gruppiere(
        [
            Eintrag("Export fehlgeschlagen\r\n   at X.Run()"),
            Eintrag("Import fehlgeschlagen\n   at X.Run()"),
            Eintrag("Export fehlgeschlagen"),
        ]);

        Assert.Equal(2, gruppen.Count);
        Assert.Equal(("Export fehlgeschlagen", 2), (gruppen[0].Meldungsmuster, gruppen[0].Anzahl));
    }
}
