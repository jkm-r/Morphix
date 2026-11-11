using Morphix.App.Dialoge;
using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests;

/// <summary>Tests des Aktionsprotokolls in der Ansicht „Dienste“ (US16). Es werden nur Fakes und Demo-Werte verwendet.</summary>
public class AktionsProtokollViewModelTests
{
    private static readonly DateTime Jetzt = new(2026, 11, 11, 16, 5, 7);

    private static readonly Systemprofil Profil = new()
    {
        Name = "Demo",
        IstDemo = true,
        Logquellen = [new Logquelle { Pfad = "x" }],
        Dienste = [new Dienst { Dienstname = "A" }],
    };

    private sealed class SpeicherProtokoll : IAktionsProtokoll
    {
        public List<Dienstaktion> Eintraege { get; } = [];
        public Exception? LeseFehler { get; set; }

        public Task ProtokolliereAsync(Dienstaktion aktion, CancellationToken abbruch = default)
        {
            Eintraege.Add(aktion);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Dienstaktion>> LeseAsync(int maximal = 100, CancellationToken abbruch = default) =>
            LeseFehler is null
                ? Task.FromResult<IReadOnlyList<Dienstaktion>>([.. Enumerable.Reverse(Eintraege).Take(maximal)])
                : Task.FromException<IReadOnlyList<Dienstaktion>>(LeseFehler);
    }

    private static (DiensteViewModel Vm, SpeicherProtokoll Protokoll) Erzeuge(bool admin = true, bool bestaetigt = true)
    {
        var gateway = Substitute.For<IDienstGateway>();
        gateway.AbfragenAsync(Arg.Any<IEnumerable<Dienst>>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult<IReadOnlyList<Dienst>>([.. c.Arg<IEnumerable<Dienst>>().Select(d => d with { Status = DienstStatus.Gestoppt })]));
        gateway.AusfuehrenAsync(Arg.Any<Dienst>(), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult(c.Arg<Dienst>() with { Status = DienstStatus.Gestartet }));
        var rechte = Substitute.For<IRechtePruefer>();
        rechte.HatAdminrechte().Returns(admin);
        var dialog = Substitute.For<IBestaetigungsDialog>();
        dialog.Bestaetigen(Arg.Any<string>(), Arg.Any<string>()).Returns(bestaetigt);
        var protokoll = new SpeicherProtokoll();
        var service = new DienstService(
            TestAufbau.FakeFabrik(new Datenquellen(
                Substitute.For<ILogLeser>(), gateway, Substitute.For<IAufgabenGateway>(), Substitute.For<IEreignisLeser>(), rechte)),
            protokoll, new FesteUhr(Jetzt), () => "tester");
        return (new DiensteViewModel(service, dialog), protokoll);
    }

    /// <summary>Nach einer Aktion erscheint sie als oberste Zeile des Protokolls mit Zeit, Benutzer, Aktion, Dienst und Ergebnis.</summary>
    [Fact]
    public async Task Aktion_erscheint_im_Protokoll()
    {
        var (vm, _) = Erzeuge();
        vm.SetzeProfil(Profil);
        await vm.AktualisierenAsync();
        vm.AusgewaehlterDienst = vm.Dienste[0];

        await vm.StartenCommand.ExecuteAsync(null);

        var zeile = Assert.Single(vm.Protokoll);
        Assert.Equal("11.11.2026 16:05:07", zeile.ZeitText);
        Assert.Equal("tester", zeile.Benutzer);
        Assert.Equal("Starten", zeile.AktionText);
        Assert.Equal("A (Demo)", zeile.DienstText);
        Assert.Equal("Erfolgreich (simuliert)", zeile.ErgebnisText);
        Assert.Contains("gestartet", zeile.Meldung);
        Assert.False(zeile.IstFehler);
    }

    /// <summary>Ein abgebrochener Bestätigungsdialog ist keine Aktion: Es wird nichts ausgeführt und nichts protokolliert.</summary>
    [Fact]
    public async Task Abgebrochener_Dialog_wird_nicht_protokolliert()
    {
        var (vm, protokoll) = Erzeuge(bestaetigt: false);
        vm.SetzeProfil(Profil);
        await vm.AktualisierenAsync();
        vm.AusgewaehlterDienst = vm.Dienste[0];

        await vm.StartenCommand.ExecuteAsync(null);

        Assert.Empty(protokoll.Eintraege);
        Assert.Empty(vm.Protokoll);
        Assert.Contains("abgebrochen", vm.Meldung);
    }

    /// <summary>Ohne Administratorrechte sind die Schaltflächen gesperrt; es entsteht kein Eintrag.</summary>
    [Fact]
    public async Task Ohne_Adminrechte_keine_Aktion_und_kein_Eintrag()
    {
        var (vm, protokoll) = Erzeuge(admin: false);
        vm.SetzeProfil(Profil);
        await vm.AktualisierenAsync();
        vm.AusgewaehlterDienst = vm.Dienste[0];

        Assert.False(vm.StartenCommand.CanExecute(null));
        Assert.Empty(protokoll.Eintraege);
    }

    /// <summary>Fehlgeschlagene Aktionen sind als Fehler gekennzeichnet (rote Darstellung).</summary>
    [Fact]
    public async Task Abgelehnte_Aktion_ist_ein_Fehler_im_Protokoll()
    {
        var (vm, protokoll) = Erzeuge();
        protokoll.Eintraege.Add(new Dienstaktion(Jetzt, DienstAktionsart.Stoppen, "tester", false)
        {
            Dienstname = "A",
            Meldung = "Für die Dienststeuerung sind Administratorrechte erforderlich.",
        });

        await vm.AktualisiereProtokollAsync();

        var zeile = Assert.Single(vm.Protokoll);
        Assert.True(zeile.IstFehler);
        Assert.Equal("Fehlgeschlagen", zeile.ErgebnisText);
        Assert.Equal("A", zeile.DienstText);
    }

    /// <summary>Die Ansicht zeigt höchstens 100 Einträge, die neuesten zuerst.</summary>
    [Fact]
    public async Task Protokoll_ist_begrenzt_und_neueste_zuerst()
    {
        var (vm, protokoll) = Erzeuge();
        for (var i = 0; i < 120; i++)
        {
            protokoll.Eintraege.Add(new Dienstaktion(Jetzt.AddMinutes(i), DienstAktionsart.Starten, "tester", true) { Dienstname = "A" });
        }

        await vm.AktualisiereProtokollAsync();

        Assert.Equal(DiensteViewModel.MaxProtokollEintraege, vm.Protokoll.Count);
        Assert.Equal(Jetzt.AddMinutes(119), vm.Protokoll[0].Aktion.Zeitpunkt);
    }

    /// <summary>Lässt sich das Protokoll nicht lesen, erscheint ein Hinweis statt eines Absturzes.</summary>
    [Fact]
    public async Task Lesefehler_wird_gemeldet()
    {
        var (vm, protokoll) = Erzeuge();
        protokoll.LeseFehler = new IOException("Datei gesperrt.");

        await vm.AktualisiereProtokollAsync();

        Assert.Contains("Datei gesperrt.", vm.ProtokollHinweis);
        Assert.Empty(vm.Protokoll);

        protokoll.LeseFehler = null;
        await vm.AktualisiereProtokollAsync();
        Assert.Equal("", vm.ProtokollHinweis);
    }
}
