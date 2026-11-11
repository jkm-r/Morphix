using Morphix.Core.Domaene;

namespace Morphix.Tests.Domaene;

/// <summary>Tests des <see cref="LogFilter"/> (US04).</summary>
public class LogFilterTests
{
    private static readonly List<Logeintrag> Daten =
    [
        new(new DateTime(2026, 11, 1, 8, 0, 0), Schweregrad.Info, "app.log", "Start des Dienstes"),
        new(new DateTime(2026, 11, 2, 9, 0, 0), Schweregrad.Warnung, "app.log", "Lizenz läuft ab"),
        new(new DateTime(2026, 11, 3, 10, 0, 0), Schweregrad.Fehler, "db.log", "Datenbankverbindung verloren"),
        new(new DateTime(2026, 11, 3, 23, 59, 59), Schweregrad.Kritisch, "db.log", "Platte VOLL"),
        new(new DateTime(2026, 11, 4, 0, 0, 0), Schweregrad.Fehler, "app.log", "Timeout"),
    ];

    private static List<string> Meldungen(IEnumerable<Logeintrag> e) => [.. e.Select(x => x.Meldung)];

    /// <summary>Ohne Kriterien kommt alles zurück.</summary>
    [Fact]
    public void Ohne_Kriterien_alles()
    {
        Assert.Equal(5, LogFilter.Anwenden(Daten).Count());
        Assert.Equal(5, LogFilter.Anwenden(Daten, null, "", null, null).Count());
        Assert.Equal(5, LogFilter.Anwenden(Daten, null, "   ").Count());
    }

    /// <summary>Der Schweregrad-Filter ist exakt.</summary>
    [Fact]
    public void Schweregrad_exakt()
    {
        Assert.Equal(["Datenbankverbindung verloren", "Timeout"], Meldungen(LogFilter.Anwenden(Daten, Schweregrad.Fehler)));
    }

    /// <summary>Der Suchbegriff sucht in Meldung und Quelle, ohne Beachtung der Schreibweise.</summary>
    [Fact]
    public void Suchbegriff_in_Meldung_und_Quelle()
    {
        Assert.Equal(["Platte VOLL"], Meldungen(LogFilter.Anwenden(Daten, suchbegriff: "voll")));
        Assert.Equal(["Datenbankverbindung verloren", "Platte VOLL"], Meldungen(LogFilter.Anwenden(Daten, suchbegriff: "DB.LOG")));
    }

    /// <summary>Der Tagesfilter schließt den ganzen letzten Tag ein, auch 23:59:59.</summary>
    [Fact]
    public void Zeitraum_auf_Tagesbasis()
    {
        var ergebnis = LogFilter.Anwenden(Daten, vonTag: new DateTime(2026, 11, 3, 15, 0, 0), bisTag: new DateTime(2026, 11, 3));

        Assert.Equal(["Datenbankverbindung verloren", "Platte VOLL"], Meldungen(ergebnis));
    }

    /// <summary>Die Kriterien sind kombinierbar (UND).</summary>
    [Fact]
    public void Kombination()
    {
        var ergebnis = LogFilter.Anwenden(
            Daten, Schweregrad.Fehler, "app", new DateTime(2026, 11, 1), new DateTime(2026, 11, 4));

        Assert.Equal(["Timeout"], Meldungen(ergebnis));
    }

    /// <summary>Der Suchbegriff darf höchstens 200 Zeichen lang sein; ein zu langer wird gemeldet und ignoriert.</summary>
    [Fact]
    public void Suchbegriff_Laenge()
    {
        var grenze = new string('x', 200);
        var zuLang = new string('x', 201);

        Assert.True(LogFilter.PruefeSuchbegriff(grenze).IstGueltig);
        Assert.True(LogFilter.PruefeSuchbegriff(null).IstGueltig);
        Assert.Equal(["Der Suchbegriff ist zu lang."], LogFilter.PruefeSuchbegriff(zuLang).MeldungenFuer("Suchbegriff"));
        Assert.Equal(5, LogFilter.Anwenden(Daten, suchbegriff: zuLang).Count());
    }

    /// <summary>Leere Eingabe ergibt ein leeres Ergebnis.</summary>
    [Fact]
    public void Leere_Eingabe()
    {
        Assert.Empty(LogFilter.Anwenden([], Schweregrad.Fehler, "x"));
    }
}
