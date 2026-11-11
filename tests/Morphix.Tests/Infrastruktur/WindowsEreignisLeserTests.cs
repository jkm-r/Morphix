using Morphix.Core.Domaene;
using Morphix.Infrastructure.Ereignisse;
using NSubstitute;

namespace Morphix.Tests.Infrastruktur;

/// <summary>
/// Tests des <see cref="WindowsEreignisLeser"/> über den Fake <see cref="IEreignisZugriff"/>. Die echte
/// Ereignisanzeige wird nie gelesen.
/// </summary>
public class WindowsEreignisLeserTests
{
    private static readonly DateTime Von = new(2026, 11, 10, 0, 0, 0);
    private static readonly DateTime Bis = new(2026, 11, 11, 0, 0, 0);
    private static readonly Logquelle Quelle = new() { Typ = LogquellenTyp.Ereignisanzeige, Pfad = "Application" };

    private static (WindowsEreignisLeser Leser, IEreignisZugriff Zugriff) Erzeuge(params RohEreignis[] ereignisse)
    {
        var zugriff = Substitute.For<IEreignisZugriff>();
        zugriff.Lesen(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ereignisse);
        return (new WindowsEreignisLeser(zugriff), zugriff);
    }

    private static RohEreignis Roh(int stunde, int stufe, string text = "Meldung", string anbieter = "Anbieter", int id = 7) =>
        new(Von.AddHours(stunde), stufe, anbieter, id, text);

    /// <summary>US06: Stufen werden auf die Schweregrade abgebildet; die Quelle ist „Ereignisanzeige“.</summary>
    [Fact]
    public async Task Bildet_Stufen_auf_Schweregrade_ab()
    {
        var (leser, _) = Erzeuge(Roh(1, 1), Roh(2, 2), Roh(3, 3));

        var ergebnis = await leser.LesenAsync(Quelle, Von, Bis);

        Assert.Equal([Schweregrad.Kritisch, Schweregrad.Fehler, Schweregrad.Warnung], ergebnis.Select(e => e.Schweregrad));
        Assert.All(ergebnis, e => Assert.Equal(Logeintrag.EreignisQuelle, e.Quelle));
    }

    /// <summary>Informationen und ausführliche Ereignisse gehören nicht in die Liste.</summary>
    [Fact]
    public async Task Ignoriert_Information_und_ausfuehrliche_Ereignisse()
    {
        var (leser, _) = Erzeuge(Roh(1, 4), Roh(2, 5), Roh(3, 0), Roh(4, 2));

        var ergebnis = await leser.LesenAsync(Quelle, Von, Bis);

        Assert.Single(ergebnis);
    }

    /// <summary>Die Meldung nennt Anbieter, ID und Text; ohne Text steht ein Ersatztext da.</summary>
    [Fact]
    public async Task Meldung_enthaelt_Anbieter_ID_und_Text()
    {
        var (leser, _) = Erzeuge(Roh(1, 2, "  Dienst beendet.  ", "Service Control Manager", 7034), Roh(2, 3, "", id: 1));
        // Leerer Text (zweites Ereignis) und fehlender Text verhalten sich gleich.
        var ergebnis = await leser.LesenAsync(Quelle, Von, Bis);

        Assert.Equal("Service Control Manager (ID 7034): Dienst beendet.", ergebnis[0].Meldung);
        Assert.Equal("Anbieter (ID 1): (keine Beschreibung verfügbar)", ergebnis[1].Meldung);
    }

    /// <summary>Der Zugriff liefert „jüngste zuerst“; die Ergebnisliste ist aufsteigend nach Zeit sortiert.</summary>
    [Fact]
    public async Task Sortiert_aufsteigend()
    {
        var (leser, _) = Erzeuge(Roh(9, 2, "spät"), Roh(2, 2, "früh"), Roh(5, 2, "mitte"));

        var ergebnis = await leser.LesenAsync(Quelle, Von, Bis);

        Assert.Equal(["früh", "mitte", "spät"], ergebnis.Select(e => e.Meldung.Split(": ")[1]));
    }

    /// <summary>Ereignisse außerhalb des Zeitraums (z. B. durch Rundung der Abfrage) werden verworfen.</summary>
    [Fact]
    public async Task Filtert_auf_den_Zeitraum()
    {
        var (leser, _) = Erzeuge(Roh(-1, 2), Roh(5, 2), Roh(25, 2));

        Assert.Single(await leser.LesenAsync(Quelle, Von, Bis));
    }

    /// <summary>Protokollname, Zeitraum und Höchstzahl gehen an den Zugriff.</summary>
    [Fact]
    public async Task Gibt_Protokoll_Zeitraum_und_Grenze_weiter()
    {
        var (leser, zugriff) = Erzeuge();

        await leser.LesenAsync(Quelle, Von, Bis);

        zugriff.Received(1).Lesen("Application", Von, Bis, WindowsEreignisLeser.MaxEreignisse, Arg.Any<CancellationToken>());
    }

    /// <summary>Ein nicht lesbares Protokoll löst eine Ausnahme aus, die der DiagnoseService als übersprungene Quelle meldet.</summary>
    [Fact]
    public async Task Fehler_des_Zugriffs_wird_weitergereicht()
    {
        var zugriff = Substitute.For<IEreignisZugriff>();
        zugriff.Lesen(Arg.Any<string>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => throw new UnauthorizedAccessException("kein Zugriff"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => new WindowsEreignisLeser(zugriff).LesenAsync(Quelle, Von, Bis));
    }
}
