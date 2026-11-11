using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure;
using NSubstitute;

namespace Morphix.Tests.Anwendung;

/// <summary>
/// Tests des <see cref="DienstService"/>: GR03 (Profil, Rechte, Bestätigung) mit Fakes und GR04/Demo mit den
/// Demo-Adaptern. Echte Windows-Dienste werden nie angefasst.
/// </summary>
public class DienstServiceTests
{
    private static readonly Systemprofil Profil = new()
    {
        Name = "Test",
        Logquellen = [new Logquelle { Pfad = "x" }],
        Dienste =
        [
            new Dienst { Dienstname = "Dienst-A", IstKritisch = true },
            new Dienst { Dienstname = "Dienst-B" },
        ],
    };

    private sealed record Aufbau(DienstService Service, IDienstGateway Gateway, IRechtePruefer Rechte);

    private static Aufbau Erzeuge(bool adminrechte = true)
    {
        var gateway = Substitute.For<IDienstGateway>();
        gateway.AusfuehrenAsync(Arg.Any<Dienst>(), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<Dienst>() with { Status = DienstStatus.Gestartet }));
        var rechte = Substitute.For<IRechtePruefer>();
        rechte.HatAdminrechte().Returns(adminrechte);
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        fabrik.Erzeugen(Arg.Any<Systemprofil>()).Returns(new Datenquellen(
            Substitute.For<ILogLeser>(), gateway, Substitute.For<IAufgabenGateway>(), Substitute.For<IEreignisLeser>(), rechte));
        return new Aufbau(new DienstService(fabrik), gateway, rechte);
    }

    private static Task<DienstAktionsErgebnis> Fuehre(Aufbau a, string name = "Dienst-A", bool bestaetigt = true,
        DienstAktionsart aktion = DienstAktionsart.Starten) =>
        a.Service.AusfuehrenAsync(Profil, new Dienst { Dienstname = name }, aktion, bestaetigt);

    /// <summary>Sind alle Bedingungen von GR03 erfüllt, wird die Aktion ausgeführt.</summary>
    [Fact]
    public async Task Fuehrt_aus_wenn_GR03_erfuellt()
    {
        var a = Erzeuge();

        var ergebnis = await Fuehre(a);

        Assert.True(ergebnis.Erfolgreich);
        Assert.Equal("Dienst „Dienst-A“ wurde gestartet.", ergebnis.Meldung);
        Assert.Equal(DienstStatus.Gestartet, ergebnis.Dienst?.Status);
        await a.Gateway.Received(1).AusfuehrenAsync(
            Arg.Is<Dienst>(d => d.Dienstname == "Dienst-A" && d.IstKritisch), DienstAktionsart.Starten, Arg.Any<CancellationToken>());
    }

    /// <summary>GR03: Ein Dienst außerhalb des Profils wird nicht gesteuert (auch nicht bei Rechten und Bestätigung).</summary>
    [Fact]
    public async Task Dienst_ausserhalb_des_Profils_wird_abgelehnt()
    {
        var a = Erzeuge();

        var ergebnis = await Fuehre(a, name: "Spooler");

        Assert.False(ergebnis.Erfolgreich);
        Assert.Contains("nicht zum Profil", ergebnis.Meldung);
        await a.Gateway.DidNotReceiveWithAnyArgs().AusfuehrenAsync(default!, default, default);
    }

    /// <summary>Der Dienstname wird ohne Beachtung der Schreibweise dem Profil zugeordnet.</summary>
    [Fact]
    public async Task Dienstname_ohne_Beachtung_der_Schreibweise()
    {
        var a = Erzeuge();

        var ergebnis = await Fuehre(a, name: "DIENST-b");

        Assert.True(ergebnis.Erfolgreich);
        await a.Gateway.Received(1).AusfuehrenAsync(Arg.Is<Dienst>(d => d.Dienstname == "Dienst-B"), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>());
    }

    /// <summary>GR03: Ohne Administratorrechte wird nichts ausgeführt – auch dann nicht, wenn die Oberfläche es versucht.</summary>
    [Fact]
    public async Task Ohne_Adminrechte_wird_abgelehnt()
    {
        var a = Erzeuge(adminrechte: false);

        var ergebnis = await Fuehre(a);

        Assert.False(ergebnis.Erfolgreich);
        Assert.Contains("Administratorrechte", ergebnis.Meldung);
        await a.Gateway.DidNotReceiveWithAnyArgs().AusfuehrenAsync(default!, default, default);
    }

    /// <summary>GR03: Ohne Bestätigung wird nichts ausgeführt.</summary>
    [Fact]
    public async Task Ohne_Bestaetigung_wird_abgelehnt()
    {
        var a = Erzeuge();

        var ergebnis = await Fuehre(a, bestaetigt: false);

        Assert.False(ergebnis.Erfolgreich);
        Assert.Contains("nicht bestätigt", ergebnis.Meldung);
        await a.Gateway.DidNotReceiveWithAnyArgs().AusfuehrenAsync(default!, default, default);
    }

    /// <summary>Alle drei Aktionen werden an das Gateway weitergereicht und mit passender Meldung bestätigt.</summary>
    [Theory]
    [InlineData(DienstAktionsart.Starten, "gestartet")]
    [InlineData(DienstAktionsart.Stoppen, "gestoppt")]
    [InlineData(DienstAktionsart.Neustarten, "neu gestartet")]
    public async Task Aktionen_und_Meldungen(DienstAktionsart aktion, string erwartet)
    {
        var a = Erzeuge();

        var ergebnis = await Fuehre(a, aktion: aktion);

        Assert.EndsWith($"wurde {erwartet}.", ergebnis.Meldung);
        await a.Gateway.Received(1).AusfuehrenAsync(Arg.Any<Dienst>(), aktion, Arg.Any<CancellationToken>());
    }

    /// <summary>Scheitert die Aktion im Adapter (Timeout, Zugriff verweigert), kommt die Meldung als Ergebnis zurück.</summary>
    [Fact]
    public async Task Fehler_des_Adapters_wird_zur_Meldung()
    {
        var a = Erzeuge();
        a.Gateway.AusfuehrenAsync(Arg.Any<Dienst>(), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>())
            .Returns<Task<Dienst>>(_ => throw new DienstAktionFehlgeschlagenException("Zeitüberschreitung nach 30 Sekunden."));

        var ergebnis = await Fuehre(a);

        Assert.False(ergebnis.Erfolgreich);
        Assert.Equal("Zeitüberschreitung nach 30 Sekunden.", ergebnis.Meldung);
        Assert.Null(ergebnis.Dienst);
    }

    /// <summary>Abfrage und Rechteprüfung gehen an die Datenquellen des Profils.</summary>
    [Fact]
    public async Task Abfrage_und_Rechte()
    {
        var a = Erzeuge(adminrechte: false);
        a.Gateway.AbfragenAsync(Arg.Any<IEnumerable<Dienst>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Dienst>>([new Dienst { Dienstname = "Dienst-A", Status = DienstStatus.Gestoppt }]));

        var dienste = await a.Service.AbfragenAsync(Profil);

        Assert.Equal(DienstStatus.Gestoppt, Assert.Single(dienste).Status);
        Assert.False(a.Service.HatAdminrechte(Profil));
    }

    // ------------------------------------------------------------- Demo-Modus (GR04)

    private static readonly string DemoOrdner = System.IO.Path.Combine(AppContext.BaseDirectory, "demo");

    private static async Task<Systemprofil> DemoProfil() =>
        (await new Morphix.Infrastructure.Profile.JsonProfilRepository("x", "y", System.IO.Path.Combine(DemoOrdner, "demo-profil.json"))
            .LadeDemoProfilAsync())!;

    /// <summary>GR04: Im Demo-Modus lassen sich Dienste ohne Administratorrechte (simuliert) steuern.</summary>
    [Fact]
    public async Task Demo_Dienste_lassen_sich_simuliert_steuern()
    {
        var profil = await DemoProfil();
        var service = new DienstService(new WindowsDatenquellenFabrik(DemoOrdner));
        Assert.True(service.HatAdminrechte(profil));

        var vorher = await service.AbfragenAsync(profil);
        var gestoppt = Assert.Single(vorher, d => d.Status == DienstStatus.Gestoppt);

        var ergebnis = await service.AusfuehrenAsync(profil, gestoppt, DienstAktionsart.Starten, bestaetigt: true);

        Assert.True(ergebnis.Erfolgreich);
        Assert.DoesNotContain(await service.AbfragenAsync(profil), d => d.Status == DienstStatus.Gestoppt);

        await service.AusfuehrenAsync(profil, gestoppt, DienstAktionsart.Stoppen, bestaetigt: true);
        Assert.Equal(DienstStatus.Gestoppt, (await service.AbfragenAsync(profil)).First(d => d.Dienstname == gestoppt.Dienstname).Status);
    }

    /// <summary>Im Demo-Modus gilt GR03 weiter: ohne Bestätigung und für Fremddienste passiert nichts.</summary>
    [Fact]
    public async Task Demo_GR03_gilt_weiter()
    {
        var profil = await DemoProfil();
        var service = new DienstService(new WindowsDatenquellenFabrik(DemoOrdner));

        var ohneBestaetigung = await service.AusfuehrenAsync(profil, profil.Dienste[0], DienstAktionsart.Stoppen, bestaetigt: false);
        var fremd = await service.AusfuehrenAsync(profil, new Dienst { Dienstname = "Spooler" }, DienstAktionsart.Stoppen, bestaetigt: true);

        Assert.False(ohneBestaetigung.Erfolgreich);
        Assert.False(fremd.Erfolgreich);
        Assert.Equal(DienstStatus.Gestartet, (await service.AbfragenAsync(profil))[0].Status);
    }

    /// <summary>Das simulierte System bleibt zwischen Dienste-Ansicht und Analyse konsistent.</summary>
    [Fact]
    public async Task Demo_Zustand_gilt_auch_fuer_die_Analyse()
    {
        var profil = await DemoProfil();
        var fabrik = new WindowsDatenquellenFabrik(DemoOrdner);
        var dienstService = new DienstService(fabrik);
        var diagnoseService = new DiagnoseService(fabrik);
        var (von, bis, _) = diagnoseService.PruefeTage(DateTime.Today.AddDays(-6), DateTime.Today);

        Assert.Single((await diagnoseService.ErstellenAsync(profil, von, bis)).Dienste, d => d.Status == DienstStatus.Gestoppt);

        var gestoppt = (await dienstService.AbfragenAsync(profil)).Single(d => d.Status == DienstStatus.Gestoppt);
        await dienstService.AusfuehrenAsync(profil, gestoppt, DienstAktionsart.Starten, bestaetigt: true);

        Assert.DoesNotContain((await diagnoseService.ErstellenAsync(profil, von, bis)).Dienste, d => d.Status == DienstStatus.Gestoppt);
    }
}
