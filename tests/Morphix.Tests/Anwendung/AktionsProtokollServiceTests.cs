using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests.Anwendung;

/// <summary>
/// Tests der Protokollierung im <see cref="DienstService"/> (US16): Jede Dienstaktion wird mit Zeit, Benutzer und Ergebnis
/// vermerkt. Echte Dienste werden nie gesteuert; Gateway, Rechteprüfung und Protokoll sind Fakes.
/// </summary>
public class AktionsProtokollServiceTests
{
    private static readonly DateTime Jetzt = new(2026, 11, 11, 15, 45, 0);

    private static readonly Systemprofil Profil = new()
    {
        Name = "Testprofil",
        Logquellen = [new Logquelle { Pfad = "x" }],
        Dienste = [new Dienst { Dienstname = "Dienst-A" }],
    };

    private sealed record Aufbau(DienstService Service, IDienstGateway Gateway, IAktionsProtokoll Protokoll, List<Dienstaktion> Eintraege);

    private static Aufbau Erzeuge(bool admin = true)
    {
        var gateway = Substitute.For<IDienstGateway>();
        gateway.AusfuehrenAsync(Arg.Any<Dienst>(), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<Dienst>() with { Status = DienstStatus.Gestartet }));
        var rechte = Substitute.For<IRechtePruefer>();
        rechte.HatAdminrechte().Returns(admin);
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        fabrik.Erzeugen(Arg.Any<Systemprofil>()).Returns(new Datenquellen(
            Substitute.For<ILogLeser>(), gateway, Substitute.For<IAufgabenGateway>(), Substitute.For<IEreignisLeser>(), rechte));

        var eintraege = new List<Dienstaktion>();
        var protokoll = Substitute.For<IAktionsProtokoll>();
        protokoll.ProtokolliereAsync(Arg.Do<Dienstaktion>(eintraege.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var service = new DienstService(fabrik, protokoll, new FesteUhr(Jetzt), () => "tester");
        return new Aufbau(service, gateway, protokoll, eintraege);
    }

    /// <summary>Eine ausgeführte Aktion wird mit Zeit, Benutzer, Art, Dienst, Profil und Ergebnis protokolliert.</summary>
    [Fact]
    public async Task Erfolgreiche_Aktion_wird_protokolliert()
    {
        var a = Erzeuge();

        var ergebnis = await a.Service.AusfuehrenAsync(Profil, new Dienst { Dienstname = "dienst-a" }, DienstAktionsart.Starten, bestaetigt: true);

        Assert.True(ergebnis.Erfolgreich);
        var eintrag = Assert.Single(a.Eintraege);
        Assert.Equal(Jetzt, eintrag.Zeitpunkt);
        Assert.Equal("tester", eintrag.Benutzer);
        Assert.Equal(DienstAktionsart.Starten, eintrag.Aktion);
        Assert.True(eintrag.Erfolgreich);
        Assert.Equal("dienst-a", eintrag.Dienstname);
        Assert.Equal("Testprofil", eintrag.Profil);
        Assert.False(eintrag.Simuliert);
        Assert.Equal(ergebnis.Meldung, eintrag.Meldung);
    }

    /// <summary>Im Demo-Modus wird die Aktion als simuliert gekennzeichnet (GR04), ohne dass der Service den Modus unterscheidet.</summary>
    [Fact]
    public async Task Demo_Aktion_ist_als_simuliert_gekennzeichnet()
    {
        var a = Erzeuge();

        await a.Service.AusfuehrenAsync(Profil with { IstDemo = true }, Profil.Dienste[0], DienstAktionsart.Stoppen, bestaetigt: true);

        Assert.True(Assert.Single(a.Eintraege).Simuliert);
    }

    /// <summary>Abgelehnte Aktionen (GR03) erscheinen ebenfalls im Protokoll, damit Versuche nachvollziehbar sind.</summary>
    [Theory]
    [InlineData("fremd", true, true, "gehört nicht zum Profil")]
    [InlineData("Dienst-A", false, true, "Administratorrechte")]
    [InlineData("Dienst-A", true, false, "nicht bestätigt")]
    public async Task Abgelehnte_Aktion_wird_protokolliert(string dienst, bool admin, bool bestaetigt, string grund)
    {
        var a = Erzeuge(admin);

        var ergebnis = await a.Service.AusfuehrenAsync(Profil, new Dienst { Dienstname = dienst }, DienstAktionsart.Neustarten, bestaetigt);

        Assert.False(ergebnis.Erfolgreich);
        var eintrag = Assert.Single(a.Eintraege);
        Assert.False(eintrag.Erfolgreich);
        Assert.Equal(DienstAktionsart.Neustarten, eintrag.Aktion);
        Assert.Contains(grund, eintrag.Meldung);
        await a.Gateway.DidNotReceiveWithAnyArgs().AusfuehrenAsync(default!, default, default);
    }

    /// <summary>Ein Fehlschlag des Gateways (z. B. Zeitüberschreitung) wird als nicht erfolgreich protokolliert.</summary>
    [Fact]
    public async Task Fehlgeschlagene_Aktion_wird_protokolliert()
    {
        var a = Erzeuge();
        a.Gateway.AusfuehrenAsync(Arg.Any<Dienst>(), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>())
            .Returns<Task<Dienst>>(_ => throw new DienstAktionFehlgeschlagenException("Zeitüberschreitung nach 30 Sekunden."));

        var ergebnis = await a.Service.AusfuehrenAsync(Profil, Profil.Dienste[0], DienstAktionsart.Starten, bestaetigt: true);

        Assert.False(ergebnis.Erfolgreich);
        var eintrag = Assert.Single(a.Eintraege);
        Assert.False(eintrag.Erfolgreich);
        Assert.Contains("Zeitüberschreitung", eintrag.Meldung);
    }

    /// <summary>Ein Abbruch und ein unerwarteter Fehler werden protokolliert und danach weitergereicht.</summary>
    [Fact]
    public async Task Abbruch_und_unerwarteter_Fehler_werden_protokolliert_und_weitergereicht()
    {
        var a = Erzeuge();
        a.Gateway.AusfuehrenAsync(Arg.Any<Dienst>(), DienstAktionsart.Starten, Arg.Any<CancellationToken>())
            .Returns<Task<Dienst>>(_ => throw new OperationCanceledException());
        a.Gateway.AusfuehrenAsync(Arg.Any<Dienst>(), DienstAktionsart.Stoppen, Arg.Any<CancellationToken>())
            .Returns<Task<Dienst>>(_ => throw new InvalidOperationException("kaputt"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => a.Service.AusfuehrenAsync(Profil, Profil.Dienste[0], DienstAktionsart.Starten, bestaetigt: true));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => a.Service.AusfuehrenAsync(Profil, Profil.Dienste[0], DienstAktionsart.Stoppen, bestaetigt: true));

        Assert.Equal(2, a.Eintraege.Count);
        Assert.All(a.Eintraege, e => Assert.False(e.Erfolgreich));
        Assert.Contains("abgebrochen", a.Eintraege[0].Meldung);
        Assert.Contains("kaputt", a.Eintraege[1].Meldung);
    }

    /// <summary>Kann das Protokoll nicht geschrieben werden, bleibt das Ergebnis der Aktion unverändert; die Meldung nennt das Problem.</summary>
    [Fact]
    public async Task Protokollfehler_aendert_das_Ergebnis_nicht()
    {
        var a = Erzeuge();
        a.Protokoll.ProtokolliereAsync(Arg.Any<Dienstaktion>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("Datenträger voll."));

        var ergebnis = await a.Service.AusfuehrenAsync(Profil, Profil.Dienste[0], DienstAktionsart.Starten, bestaetigt: true);

        Assert.True(ergebnis.Erfolgreich);
        Assert.NotNull(ergebnis.Dienst);
        Assert.EndsWith(DienstService.ProtokollFehlerHinweis, ergebnis.Meldung);
    }

    /// <summary>Ohne Protokoll arbeitet der Service wie bisher; das Lesen liefert eine leere Liste.</summary>
    [Fact]
    public async Task Ohne_Protokoll_funktioniert_der_Service()
    {
        var gateway = Substitute.For<IDienstGateway>();
        gateway.AusfuehrenAsync(Arg.Any<Dienst>(), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<Dienst>()));
        var rechte = Substitute.For<IRechtePruefer>();
        rechte.HatAdminrechte().Returns(true);
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        fabrik.Erzeugen(Arg.Any<Systemprofil>()).Returns(new Datenquellen(
            Substitute.For<ILogLeser>(), gateway, Substitute.For<IAufgabenGateway>(), Substitute.For<IEreignisLeser>(), rechte));
        var service = new DienstService(fabrik);

        var ergebnis = await service.AusfuehrenAsync(Profil, Profil.Dienste[0], DienstAktionsart.Starten, bestaetigt: true);

        Assert.True(ergebnis.Erfolgreich);
        Assert.Empty(await service.ProtokollLesenAsync());
    }

    /// <summary>Das Lesen reicht Anzahl und Ergebnis des Protokolls durch.</summary>
    [Fact]
    public async Task Lesen_reicht_das_Protokoll_durch()
    {
        var a = Erzeuge();
        var vorhanden = new Dienstaktion(Jetzt, DienstAktionsart.Starten, "tester", true);
        a.Protokoll.LeseAsync(25, Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<Dienstaktion>>([vorhanden]));

        Assert.Equal([vorhanden], await a.Service.ProtokollLesenAsync(25));
    }

    /// <summary>Der Benutzer stammt standardmäßig vom angemeldeten Windows-Benutzer.</summary>
    [Fact]
    public async Task Benutzer_ist_standardmaessig_der_angemeldete_Benutzer()
    {
        var a = Erzeuge();
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        var rechte = Substitute.For<IRechtePruefer>();
        rechte.HatAdminrechte().Returns(false);
        fabrik.Erzeugen(Arg.Any<Systemprofil>()).Returns(new Datenquellen(
            Substitute.For<ILogLeser>(), Substitute.For<IDienstGateway>(), Substitute.For<IAufgabenGateway>(), Substitute.For<IEreignisLeser>(), rechte));
        var service = new DienstService(fabrik, a.Protokoll);

        await service.AusfuehrenAsync(Profil, Profil.Dienste[0], DienstAktionsart.Starten, bestaetigt: true);

        Assert.Equal(Environment.UserName, Assert.Single(a.Eintraege).Benutzer);
    }
}
