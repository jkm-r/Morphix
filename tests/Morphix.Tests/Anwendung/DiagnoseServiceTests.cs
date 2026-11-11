using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests.Anwendung;

/// <summary>Tests des <see cref="DiagnoseService"/> mit NSubstitute-Fakes (P2, P3, P6, GR01, GR09).</summary>
public class DiagnoseServiceTests
{
    private static readonly DateTime Jetzt = new(2026, 11, 11, 12, 0, 0);
    private static readonly DateTime Von = new(2026, 11, 5, 0, 0, 0);

    private sealed record Aufbau(DiagnoseService Service, Datenquellen Quellen, IDatenquellenFabrik Fabrik);

    private static Aufbau Erzeuge()
    {
        var quellen = new Datenquellen(
            Substitute.For<ILogLeser>(),
            Substitute.For<IDienstGateway>(),
            Substitute.For<IAufgabenGateway>(),
            Substitute.For<IEreignisLeser>());
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        fabrik.Erzeugen(Arg.Any<Systemprofil>()).Returns(quellen);

        quellen.Dienste.AbfragenAsync(Arg.Any<IEnumerable<Dienst>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<Dienst>>([.. call.Arg<IEnumerable<Dienst>>()]));
        quellen.Aufgaben.AbfragenAsync(Arg.Any<IEnumerable<GeplanteAufgabe>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<GeplanteAufgabe>>([.. call.Arg<IEnumerable<GeplanteAufgabe>>()]));

        return new Aufbau(new DiagnoseService(fabrik, new FesteUhr(Jetzt)), quellen, fabrik);
    }

    private static Systemprofil Profil(params Logquelle[] quellen) => new()
    {
        Name = "Test",
        Logquellen = [.. quellen],
        Dienste = [new Dienst { Dienstname = "A", IstKritisch = true }],
        Aufgaben = [new GeplanteAufgabe { Name = "Sicherung" }],
    };

    private static Logeintrag Fehler(string meldung, int stunde, string quelle = "a.log") =>
        new(Von.AddHours(stunde), Schweregrad.Fehler, quelle, meldung);

    private static void LiefereLogs(Aufbau a, Logquelle quelle, LogleseErgebnis ergebnis) =>
        a.Quellen.LogLeser
            .LesenAsync(quelle, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(ergebnis);

    /// <summary>Die Diagnose ist abgeschlossen und enthält Logs, Gruppen, Dienste und Aufgaben (GR09, P7).</summary>
    [Fact]
    public async Task Erstellt_abgeschlossene_Diagnose()
    {
        var a = Erzeuge();
        var quelle = new Logquelle { Pfad = "x" };
        LiefereLogs(a, quelle, new LogleseErgebnis(
            [Fehler("Timeout 1", 3), Fehler("Timeout 2", 1), new(Von, Schweregrad.Info, "a.log", "ok")],
            2, 2048, [new UebersprungeneDatei("kaputt.log", "gesperrt")], ["a.log", "b.log"]));
        var profil = Profil(quelle);

        var d = await a.Service.ErstellenAsync(profil, Von, Von.AddDays(2));

        Assert.True(d.IstAbgeschlossen);
        Assert.Equal(profil, d.Profil);
        Assert.Equal(Jetzt, d.ErstelltAm);
        Assert.Equal(3, d.Logeintraege.Count);
        Assert.Equal(d.Logeintraege.OrderBy(e => e.Zeitstempel), d.Logeintraege);
        var gruppe = Assert.Single(d.Fehlergruppen);
        Assert.Equal(("Timeout #", 2), (gruppe.Meldungsmuster, gruppe.Anzahl));
        Assert.Single(d.Dienste);
        Assert.Single(d.Aufgaben);
        Assert.Equal(["a.log", "b.log"], d.GeleseneDateien);
        Assert.Equal("kaputt.log", Assert.Single(d.UebersprungeneDateien).Pfad);
        Assert.Equal(2048, d.GeleseneBytes);
    }

    /// <summary>GR01: Ein ungültiger Zeitraum wird abgelehnt, bevor Datenquellen angefasst werden.</summary>
    [Theory]
    [InlineData(0, 0)]   // von == bis
    [InlineData(5, 1)]   // von nach bis
    public async Task Ungueltiger_Zeitraum_wird_abgelehnt(int vonOffset, int bisOffset)
    {
        var a = Erzeuge();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            a.Service.ErstellenAsync(Profil(new Logquelle()), Von.AddDays(vonOffset), Von.AddDays(bisOffset)));
        a.Fabrik.DidNotReceiveWithAnyArgs().Erzeugen(default!);
    }

    /// <summary>GR01: Ende in der Zukunft und mehr als 30 Tage werden abgelehnt.</summary>
    [Fact]
    public async Task Zukunft_und_zu_lang_werden_abgelehnt()
    {
        var a = Erzeuge();
        var profil = Profil(new Logquelle());

        await Assert.ThrowsAsync<ArgumentException>(() => a.Service.ErstellenAsync(profil, Jetzt.AddDays(-1), Jetzt.AddSeconds(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => a.Service.ErstellenAsync(profil, Jetzt.AddDays(-31), Jetzt));
    }

    /// <summary>Tage werden in einen Zeitraum umgerechnet und geprüft (Tagesbasis, höchstens jetzt).</summary>
    [Fact]
    public void PruefeTage()
    {
        var a = Erzeuge();

        var (von, bis, ok) = a.Service.PruefeTage(Jetzt.Date.AddDays(-6), Jetzt.Date);
        Assert.True(ok.IstGueltig);
        Assert.Equal(Jetzt.Date.AddDays(-6), von);
        Assert.Equal(Jetzt, bis);

        Assert.False(a.Service.PruefeTage(Jetzt.Date.AddDays(-40), Jetzt.Date).Ergebnis.IstGueltig);
        Assert.False(a.Service.PruefeTage(Jetzt.Date, Jetzt.Date.AddDays(-1)).Ergebnis.IstGueltig);
    }

    /// <summary>Mehrere Logquellen werden zusammengeführt; Ereignisanzeige-Quellen gehen an den Ereignisleser.</summary>
    [Fact]
    public async Task Fuehrt_mehrere_Quellen_zusammen()
    {
        var a = Erzeuge();
        var datei = new Logquelle { Pfad = "x" };
        var ereignis = new Logquelle { Typ = LogquellenTyp.Ereignisanzeige, Pfad = "Application" };
        LiefereLogs(a, datei, new LogleseErgebnis([Fehler("Datei", 5)], 1, 10, [], ["a.log"]));
        a.Quellen.Ereignisse
            .LesenAsync(ereignis, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Logeintrag>>([Fehler("Ereignis", 2, "Ereignisanzeige")]));

        var d = await a.Service.ErstellenAsync(Profil(datei, ereignis), Von, Von.AddDays(1));

        Assert.Equal(["Ereignis", "Datei"], d.Logeintraege.Select(e => e.Meldung));
        Assert.Equal(2, d.Fehlergruppen.Count);
        Assert.Equal(["a.log"], d.GeleseneDateien);
    }

    /// <summary>Der Fortschritt wird gemeldet und endet bei 100.</summary>
    [Fact]
    public async Task Meldet_Fortschritt()
    {
        var a = Erzeuge();
        var quelle = new Logquelle { Pfad = "x" };
        LiefereLogs(a, quelle, LogleseErgebnis.Leer);
        var werte = new List<int>();

        await a.Service.ErstellenAsync(Profil(quelle), Von, Von.AddDays(1), new Sammler(werte));

        Assert.Equal(0, werte[0]);
        Assert.Equal(100, werte[^1]);
        Assert.Equal(werte.Order(), werte);
    }

    /// <summary>Ohne Logquellen entsteht trotzdem eine Diagnose (Dienste und Aufgaben werden erfasst).</summary>
    [Fact]
    public async Task Ohne_Logquellen()
    {
        var a = Erzeuge();

        var d = await a.Service.ErstellenAsync(Profil(), Von, Von.AddDays(1));

        Assert.True(d.IstAbgeschlossen);
        Assert.Empty(d.Logeintraege);
        Assert.Single(d.Dienste);
    }

    /// <summary>Dienste und Aufgaben werden parallel zu den Logs abgefragt, nicht erst danach (P7).</summary>
    [Fact]
    public async Task Fragt_Dienste_und_Aufgaben_parallel_ab()
    {
        var a = Erzeuge();
        var quelle = new Logquelle { Pfad = "x" };
        var logFreigabe = new TaskCompletionSource<LogleseErgebnis>();
        a.Quellen.LogLeser
            .LesenAsync(quelle, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(logFreigabe.Task);

        var lauf = a.Service.ErstellenAsync(Profil(quelle), Von, Von.AddDays(1));

        // Das Log ist noch nicht fertig, die anderen beiden Abfragen wurden aber schon gestartet.
        await a.Quellen.Dienste.Received(1).AbfragenAsync(Arg.Any<IEnumerable<Dienst>>(), Arg.Any<CancellationToken>());
        await a.Quellen.Aufgaben.Received(1).AbfragenAsync(Arg.Any<IEnumerable<GeplanteAufgabe>>(), Arg.Any<CancellationToken>());
        Assert.False(lauf.IsCompleted);

        logFreigabe.SetResult(LogleseErgebnis.Leer);
        Assert.True((await lauf).IstAbgeschlossen);
    }

    private sealed class Sammler(List<int> werte) : IProgress<int>
    {
        public void Report(int value) => werte.Add(value);
    }
}
