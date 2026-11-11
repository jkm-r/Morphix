using Morphix.App.Dialoge;
using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests;

/// <summary>Tests des <see cref="MainViewModel"/>: Navigation und Profilverwaltung.</summary>
public class MainViewModelTests
{
    private static readonly Systemprofil Demo = Profil("Demo", istDemo: true);
    private static readonly Systemprofil Echt = Profil("Echtsystem", istDemo: false);

    private static Systemprofil Profil(string name, bool istDemo) => new()
    {
        Name = name,
        IstDemo = istDemo,
        Logquellen = [new Logquelle { Pfad = "logs" }],
        Dienste = [new Dienst { Dienstname = "Dienst" }],
    };

    private sealed record Aufbau(MainViewModel Vm, IProfilRepository Repository, IProfilDialogService Dialoge);

    private static Aufbau Erzeuge(params Systemprofil[] vorhandene)
    {
        var repository = Substitute.For<IProfilRepository>();
        repository.LadenAlleAsync(Arg.Any<CancellationToken>()).Returns(vorhandene);
        var dialoge = Substitute.For<IProfilDialogService>();
        var service = new ProfilService(repository, new ProfilValidator(_ => true));
        var diagnose = new DiagnoseService(TestAufbau.FakeFabrik(), new FesteUhr(new DateTime(2026, 11, 11, 12, 0, 0)));
        var vm = new MainViewModel(service, diagnose, dialoge, new DashboardViewModel(), new LogsUndFehlerViewModel(),
            TestAufbau.Dienste(), new AufgabenViewModel(), TestAufbau.Bericht());
        return new Aufbau(vm, repository, dialoge);
    }

    /// <summary>Beim Start ist das Dashboard sichtbar.</summary>
    [Fact]
    public void Start_zeigt_Dashboard()
    {
        Assert.IsType<DashboardViewModel>(Erzeuge().Vm.AktuelleAnsicht);
    }

    /// <summary>Die Auswahl eines Navigationseintrags wechselt die aktuelle Ansicht.</summary>
    [Theory]
    [InlineData(1, typeof(LogsUndFehlerViewModel))]
    [InlineData(2, typeof(DiensteViewModel))]
    [InlineData(3, typeof(AufgabenViewModel))]
    [InlineData(4, typeof(BerichtViewModel))]
    public void Auswahl_wechselt_Ansicht(int index, Type erwartet)
    {
        var vm = Erzeuge().Vm;

        vm.AusgewaehlterEintrag = vm.Navigation[index];

        Assert.IsType(erwartet, vm.AktuelleAnsicht);
    }

    /// <summary>Die Navigation enthält die fünf Ansichten aus der Spezifikation.</summary>
    [Fact]
    public void Navigation_enthaelt_fuenf_Ansichten()
    {
        var titel = Erzeuge().Vm.Navigation.Select(e => e.Titel);

        Assert.Equal(["Dashboard", "Logs und Fehler", "Dienste", "Aufgaben", "Bericht"], titel);
    }

    /// <summary>Beim Start werden die Profile geladen; das zuletzt genutzte wird ausgewählt (US01).</summary>
    [Fact]
    public async Task Initialisieren_waehlt_zuletzt_genutztes_Profil()
    {
        var a = Erzeuge(Demo, Echt);
        a.Repository.LadeZuletztGenutztesAsync(Arg.Any<CancellationToken>()).Returns("Echtsystem");

        await a.Vm.InitialisierenAsync();

        Assert.Equal(2, a.Vm.Profile.Count);
        Assert.Equal("Echtsystem", a.Vm.AusgewaehltesProfil?.Name);
        Assert.False(a.Vm.IstDemoModus);
    }

    /// <summary>Das Demo-Profil zeigt den Demo-Hinweis an (US02).</summary>
    [Fact]
    public async Task Demo_Profil_schaltet_Demo_Hinweis_ein()
    {
        var a = Erzeuge(Demo);

        await a.Vm.InitialisierenAsync();

        Assert.True(a.Vm.IstDemoModus);
    }

    /// <summary>Ohne Profile erscheint beim ersten Start das Demo-Profil und eine Hinweismeldung.</summary>
    [Fact]
    public async Task Erster_Start_legt_Demo_Profil_an()
    {
        var a = Erzeuge();
        a.Repository.LadeDemoProfilAsync(Arg.Any<CancellationToken>()).Returns(Demo);

        await a.Vm.InitialisierenAsync();

        Assert.Equal("Demo", a.Vm.AusgewaehltesProfil?.Name);
        Assert.Contains("Demo-Profil", a.Vm.Statusmeldung);
    }

    /// <summary>Ein Lesefehler beim Start führt zu einer Statusmeldung statt zum Absturz (NFA07).</summary>
    [Fact]
    public async Task Ladefehler_wird_gemeldet()
    {
        var a = Erzeuge();
        a.Repository.LadenAlleAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Systemprofil>>(_ => throw new IOException("gesperrt"));

        await a.Vm.InitialisierenAsync();

        Assert.Contains("gesperrt", a.Vm.Statusmeldung);
    }

    /// <summary>„Bearbeiten“ ist ohne gewähltes Profil deaktiviert.</summary>
    [Fact]
    public async Task Bearbeiten_braucht_ein_Profil()
    {
        var leer = Erzeuge();
        Assert.False(leer.Vm.ProfilBearbeitenCommand.CanExecute(null));

        var mitProfil = Erzeuge(Echt);
        await mitProfil.Vm.InitialisierenAsync();
        Assert.True(mitProfil.Vm.ProfilBearbeitenCommand.CanExecute(null));
    }

    /// <summary>Ein im Dialog gespeichertes Profil wird in die Liste aufgenommen und ausgewählt.</summary>
    [Fact]
    public async Task Gespeichertes_Profil_wird_ausgewaehlt()
    {
        var a = Erzeuge(Echt);
        await a.Vm.InitialisierenAsync();
        a.Dialoge.Bearbeiten(null).Returns(call =>
        {
            // Der Dialog speichert über den Service; hier genügt das Ergebnis des Dialogs.
            return Profil("Neu", false);
        });

        a.Vm.ProfilNeuCommand.Execute(null);

        Assert.Equal("Neu", a.Vm.AusgewaehltesProfil?.Name);
        Assert.Contains("gespeichert", a.Vm.Statusmeldung);
    }

    /// <summary>Bricht der Anwender den Dialog ab, bleibt alles unverändert.</summary>
    [Fact]
    public async Task Abbruch_aendert_nichts()
    {
        var a = Erzeuge(Echt);
        await a.Vm.InitialisierenAsync();
        a.Dialoge.Bearbeiten(Arg.Any<Systemprofil?>()).Returns((Systemprofil?)null);

        a.Vm.ProfilBearbeitenCommand.Execute(null);

        Assert.Equal("Echtsystem", a.Vm.AusgewaehltesProfil?.Name);
    }

    /// <summary>Ohne Administratorrechte erscheint der Hinweis in der Statusleiste, mit Rechten nicht (US08).</summary>
    [Theory]
    [InlineData(false, "Ohne Administratorrechte: Dienststeuerung gesperrt")]
    [InlineData(true, "")]
    public async Task Rechtehinweis_in_der_Statusleiste(bool admin, string erwartet)
    {
        var repository = Substitute.For<IProfilRepository>();
        repository.LadenAlleAsync(Arg.Any<CancellationToken>()).Returns(new[] { Profil("Echtsystem", istDemo: false) });
        var rechte = Substitute.For<IRechtePruefer>();
        rechte.HatAdminrechte().Returns(admin);
        var quellen = TestAufbau.FakeDatenquellen() with { Rechte = rechte };
        var fabrik = TestAufbau.FakeFabrik(quellen);
        var dienste = new DiensteViewModel(new DienstService(fabrik), Substitute.For<IBestaetigungsDialog>());
        var vm = new MainViewModel(new ProfilService(repository, new ProfilValidator(_ => true)), new DiagnoseService(fabrik),
            Substitute.For<IProfilDialogService>(), new DashboardViewModel(), new LogsUndFehlerViewModel(), dienste,
            new AufgabenViewModel(), TestAufbau.Bericht());
        Assert.Equal("", vm.RechteHinweis); // ohne Profil kein Hinweis

        await vm.InitialisierenAsync();

        Assert.Equal(erwartet, vm.RechteHinweis);
    }

    /// <summary>Die Dienste werden nur aktualisiert, solange ihre Ansicht sichtbar ist.</summary>
    [Fact]
    public async Task Dienste_sind_nur_in_ihrer_Ansicht_aktiv()
    {
        var dienste = TestAufbau.Dienste();
        var vm = new MainViewModel(new ProfilService(Substitute.For<IProfilRepository>(), new ProfilValidator(_ => true)),
            new DiagnoseService(TestAufbau.FakeFabrik()), Substitute.For<IProfilDialogService>(), new DashboardViewModel(),
            new LogsUndFehlerViewModel(), dienste, new AufgabenViewModel(), TestAufbau.Bericht());
        await Task.CompletedTask;

        Assert.False(dienste.IstAktiv);
        vm.AusgewaehlterEintrag = vm.Navigation[2];
        Assert.True(dienste.IstAktiv);
        vm.AusgewaehlterEintrag = vm.Navigation[0];
        Assert.False(dienste.IstAktiv);
    }

    /// <summary>Der DI-Container kann das Hauptfenster-ViewModel auflösen.</summary>
    [Fact]
    public void Container_loest_MainViewModel_auf()
    {
        using var container = Morphix.App.App.BaueContainer(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "morphix-test-" + Guid.NewGuid().ToString("N")));

        Assert.NotNull(container.GetService(typeof(MainViewModel)));
        Assert.NotNull(container.GetService(typeof(IDatenquellenFabrik)));
    }
}
