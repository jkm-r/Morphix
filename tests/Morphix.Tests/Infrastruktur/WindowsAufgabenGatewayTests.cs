using Morphix.Core.Domaene;
using Morphix.Infrastructure.Aufgaben;
using NSubstitute;

namespace Morphix.Tests.Infrastruktur;

/// <summary>
/// Tests des <see cref="WindowsAufgabenGateway"/> über den Fake <see cref="IAufgabenZugriff"/>. Die echte
/// Aufgabenplanung wird nie angefasst.
/// </summary>
public class WindowsAufgabenGatewayTests
{
    private static readonly DateTime Lauf = new(2026, 11, 10, 2, 0, 0);

    private static (WindowsAufgabenGateway Gateway, IAufgabenZugriff Zugriff) Erzeuge()
    {
        var zugriff = Substitute.For<IAufgabenZugriff>();
        return (new WindowsAufgabenGateway(zugriff), zugriff);
    }

    private static GeplanteAufgabe Aufgabe(string name = "Sicherung") => new() { Name = name };

    /// <summary>US10: Letzter Lauf, Ergebnis und nächster Lauf werden übernommen.</summary>
    [Fact]
    public async Task Uebernimmt_Lauf_Ergebnis_und_naechsten_Lauf()
    {
        var (gateway, zugriff) = Erzeuge();
        zugriff.Lesen("Sicherung").Returns(new AufgabenZustand(Lauf, 0, Lauf.AddDays(1)));

        var ergebnis = Assert.Single(await gateway.AbfragenAsync([Aufgabe()]));

        Assert.Equal(Lauf, ergebnis.LetzterLauf);
        Assert.Equal(0, ergebnis.LetztesErgebnis);
        Assert.Equal(Lauf.AddDays(1), ergebnis.NaechsterLauf);
        Assert.False(ergebnis.IstFehlgeschlagen);
    }

    /// <summary>GR08: Ein Ergebniscode ungleich 0 bedeutet fehlgeschlagen (auch Windows-Fehlercodes als negative Zahl).</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(-2147024891)] // 0x80070005
    public async Task Ergebniscode_ungleich_0_ist_fehlgeschlagen(int code)
    {
        var (gateway, zugriff) = Erzeuge();
        zugriff.Lesen("Sicherung").Returns(new AufgabenZustand(Lauf, code, null));

        var ergebnis = Assert.Single(await gateway.AbfragenAsync([Aufgabe()]));

        Assert.Equal(code, ergebnis.LetztesErgebnis);
        Assert.True(ergebnis.IstFehlgeschlagen);
    }

    /// <summary>Eine nie gelaufene, laufende oder wartende Aufgabe hat kein Ergebnis und gilt nicht als fehlgeschlagen.</summary>
    [Theory]
    [InlineData(0x41303)] // noch nicht gelaufen
    [InlineData(0x41301)] // läuft
    [InlineData(0x41325)] // wartet
    public async Task Statuscodes_ohne_Lauf_sind_kein_Fehler(int code)
    {
        var (gateway, zugriff) = Erzeuge();
        zugriff.Lesen("Sicherung").Returns(new AufgabenZustand(Lauf, code, Lauf.AddDays(1)));

        var ergebnis = Assert.Single(await gateway.AbfragenAsync([Aufgabe()]));

        Assert.Null(ergebnis.LetztesErgebnis);
        Assert.False(ergebnis.IstFehlgeschlagen);
        Assert.Equal(Lauf.AddDays(1), ergebnis.NaechsterLauf);
    }

    /// <summary>Ohne letzten Lauf ist der Rohcode bedeutungslos, auch wenn er 0 ist.</summary>
    [Fact]
    public async Task Ohne_Lauf_kein_Ergebnis()
    {
        var (gateway, zugriff) = Erzeuge();
        zugriff.Lesen("Sicherung").Returns(new AufgabenZustand(null, 0, Lauf));

        var ergebnis = Assert.Single(await gateway.AbfragenAsync([Aufgabe()]));

        Assert.Null(ergebnis.LetzterLauf);
        Assert.Null(ergebnis.LetztesErgebnis);
    }

    /// <summary>Eine Aufgabe, die das System nicht kennt, kommt unverändert zurück.</summary>
    [Fact]
    public async Task Unbekannte_Aufgabe_bleibt_unveraendert()
    {
        var (gateway, zugriff) = Erzeuge();
        zugriff.Lesen("Fehlt").Returns((AufgabenZustand?)null);
        var aufgabe = Aufgabe("Fehlt");

        var ergebnis = Assert.Single(await gateway.AbfragenAsync([aufgabe]));

        Assert.Equal(aufgabe, ergebnis);
    }

    /// <summary>NFA07: Eine nicht lesbare Aufgabe bricht die Abfrage der übrigen nicht ab.</summary>
    [Fact]
    public async Task Fehler_bei_einer_Aufgabe_betrifft_die_anderen_nicht()
    {
        var (gateway, zugriff) = Erzeuge();
        zugriff.Lesen("Kaputt").Returns(_ => throw new UnauthorizedAccessException("kein Zugriff"));
        zugriff.Lesen("Gut").Returns(new AufgabenZustand(Lauf, 0, null));

        var ergebnis = await gateway.AbfragenAsync([Aufgabe("Kaputt"), Aufgabe("Gut")]);

        Assert.Equal(["Kaputt", "Gut"], ergebnis.Select(a => a.Name));
        Assert.Null(ergebnis[0].LetztesErgebnis);
        Assert.Equal(0, ergebnis[1].LetztesErgebnis);
    }

    /// <summary>Die Reihenfolge des Profils bleibt erhalten, ohne Aufgaben kommt eine leere Liste.</summary>
    [Fact]
    public async Task Reihenfolge_und_leere_Liste()
    {
        var (gateway, zugriff) = Erzeuge();
        zugriff.Lesen(Arg.Any<string>()).Returns(new AufgabenZustand(Lauf, 0, null));

        Assert.Equal(["C", "A", "B"], (await gateway.AbfragenAsync([Aufgabe("C"), Aufgabe("A"), Aufgabe("B")])).Select(a => a.Name));
        Assert.Empty(await gateway.AbfragenAsync([]));
    }

    /// <summary>Ein Abbruch wird weitergegeben.</summary>
    [Fact]
    public async Task Abbruch_wird_weitergegeben()
    {
        var (gateway, _) = Erzeuge();
        using var abbruch = new CancellationTokenSource();
        await abbruch.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.AbfragenAsync([Aufgabe()], abbruch.Token));
    }
}
