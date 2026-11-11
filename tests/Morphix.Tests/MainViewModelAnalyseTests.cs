using Morphix.App.Dialoge;
using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests;

/// <summary>Tests von Zeitraum und Analyse im <see cref="MainViewModel"/> (M4).</summary>
public class MainViewModelAnalyseTests
{
    private static readonly DateTime Jetzt = new(2026, 11, 11, 12, 0, 0);

    private static readonly Systemprofil Profil = new()
    {
        Name = "Test",
        Logquellen = [new Logquelle { Pfad = "x" }],
        Dienste = [new Dienst { Dienstname = "D", IstKritisch = true }],
    };

    private sealed record Aufbau(MainViewModel Vm, DashboardViewModel Dashboard, LogsUndFehlerViewModel Logs, ILogLeser LogLeser);

    private static async Task<Aufbau> ErzeugeAsync(Func<NSubstitute.Core.CallInfo, Task<LogleseErgebnis>>? logs = null)
    {
        var repository = Substitute.For<IProfilRepository>();
        repository.LadenAlleAsync(Arg.Any<CancellationToken>()).Returns(new[] { Profil });

        var logLeser = Substitute.For<ILogLeser>();
        logLeser.LesenAsync(Arg.Any<Logquelle>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>())
            .Returns(logs ?? (_ => Task.FromResult(new LogleseErgebnis(
                [new Logeintrag(Jetzt.AddHours(-2), Schweregrad.Fehler, "a.log", "Fehler 1"), new Logeintrag(Jetzt.AddHours(-1), Schweregrad.Fehler, "a.log", "Fehler 2")],
                1, 1536, [], ["a.log"]))));
        var dienste = Substitute.For<IDienstGateway>();
        dienste.AbfragenAsync(Arg.Any<IEnumerable<Dienst>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<Dienst>>([new Dienst { Dienstname = "D", IstKritisch = true, Status = DienstStatus.Gestoppt }]));
        var aufgaben = Substitute.For<IAufgabenGateway>();
        aufgaben.AbfragenAsync(Arg.Any<IEnumerable<GeplanteAufgabe>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<GeplanteAufgabe>>([]));
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        fabrik.Erzeugen(Arg.Any<Systemprofil>()).Returns(new Datenquellen(logLeser, dienste, aufgaben, Substitute.For<IEreignisLeser>(), Substitute.For<IRechtePruefer>()));

        var dashboard = new DashboardViewModel();
        var logsVm = new LogsUndFehlerViewModel();
        var vm = new MainViewModel(
            new ProfilService(repository, new ProfilValidator(_ => true)),
            new DiagnoseService(fabrik, new FesteUhr(Jetzt)),
            Substitute.For<IProfilDialogService>(),
            dashboard, logsVm, TestAufbau.Dienste(), new AufgabenViewModel(), TestAufbau.Bericht());
        await vm.InitialisierenAsync();
        return new Aufbau(vm, dashboard, logsVm, logLeser);
    }

    /// <summary>Der Standardzeitraum sind die letzten 7 Tage (heute und die 6 Tage davor) und ist gültig.</summary>
    [Fact]
    public async Task Standardzeitraum_sind_sieben_Tage()
    {
        var vm = (await ErzeugeAsync()).Vm;

        // Die Standardwerte stammen aus der Systemuhr; die Prüfung nutzt die Uhr des Service.
        Assert.Equal(DateTime.Today, vm.ZeitraumBis);
        Assert.Equal(DateTime.Today.AddDays(-6), vm.ZeitraumVon);
        Assert.Equal(7, (vm.ZeitraumBis!.Value - vm.ZeitraumVon!.Value).Days + 1);
    }

    /// <summary>GR01 am Feld: ungültiger Zeitraum zeigt die Meldung und sperrt „Analyse starten“.</summary>
    [Fact]
    public async Task Ungueltiger_Zeitraum_sperrt_die_Analyse()
    {
        var vm = (await ErzeugeAsync()).Vm;
        vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        vm.ZeitraumBis = Jetzt.Date;
        Assert.Equal("", vm.ZeitraumFehler);
        Assert.True(vm.AnalyseStartenCommand.CanExecute(null));

        vm.ZeitraumVon = Jetzt.Date.AddDays(-40);
        Assert.Equal("Der Zeitraum ist ungültig (max. 30 Tage, nicht in der Zukunft).", vm.ZeitraumFehler);
        Assert.False(vm.AnalyseStartenCommand.CanExecute(null));

        vm.ZeitraumVon = Jetzt.Date.AddDays(1);
        vm.ZeitraumBis = Jetzt.Date.AddDays(2);
        Assert.NotEmpty(vm.ZeitraumFehler);

        vm.ZeitraumVon = null;
        Assert.NotEmpty(vm.ZeitraumFehler);
        Assert.False(vm.AnalyseStartenCommand.CanExecute(null));
    }

    /// <summary>Genau 30 Kalendertage sind erlaubt, 31 nicht.</summary>
    [Fact]
    public async Task Dreissig_Tage_Grenze_in_der_Oberflaeche()
    {
        var vm = (await ErzeugeAsync()).Vm;
        vm.ZeitraumBis = Jetzt.Date;

        vm.ZeitraumVon = Jetzt.Date.AddDays(-29);
        Assert.Equal("", vm.ZeitraumFehler);

        vm.ZeitraumVon = Jetzt.Date.AddDays(-30);
        Assert.NotEmpty(vm.ZeitraumFehler);
    }

    /// <summary>Ohne Profil kann keine Analyse starten.</summary>
    [Fact]
    public async Task Ohne_Profil_keine_Analyse()
    {
        var vm = (await ErzeugeAsync()).Vm;
        vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        vm.ZeitraumBis = Jetzt.Date;

        vm.AusgewaehltesProfil = null;

        Assert.False(vm.AnalyseStartenCommand.CanExecute(null));
    }

    /// <summary>Die Analyse füllt Dashboard und Logansicht und meldet Dateianzahl und Größe in der Statusleiste (US03).</summary>
    [Fact]
    public async Task Analyse_fuellt_Dashboard_und_Logs()
    {
        var a = await ErzeugeAsync();
        a.Vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        a.Vm.ZeitraumBis = Jetzt.Date;

        await a.Vm.AnalyseStartenCommand.ExecuteAsync(null);

        Assert.Equal("2", a.Dashboard.FehlerAnzahl);
        Assert.Equal("1", a.Dashboard.GestoppteKritischeDienste);
        Assert.Equal(2, a.Logs.Eintraege.Count);
        Assert.Equal("Analyse abgeschlossen · 1 Dateien (1,5 KB) eingelesen", a.Vm.Statusmeldung);
        Assert.False(a.Vm.IstAnalyseLaeuft);
        Assert.True(a.Vm.AnalyseStartenCommand.CanExecute(null));
    }

    /// <summary>Der Zeitraum wird tageweise übergeben: von 00:00:00, bis höchstens jetzt.</summary>
    [Fact]
    public async Task Analyse_uebergibt_Tageszeitraum()
    {
        var a = await ErzeugeAsync();
        a.Vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        a.Vm.ZeitraumBis = Jetzt.Date;

        await a.Vm.AnalyseStartenCommand.ExecuteAsync(null);

        await a.LogLeser.Received(1).LesenAsync(
            Arg.Any<Logquelle>(), Jetzt.Date.AddDays(-6), Jetzt, Arg.Any<IProgress<int>?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Während die Analyse läuft, ist der Button gesperrt und die Fortschrittsanzeige aktiv.</summary>
    [Fact]
    public async Task Waehrend_der_Analyse_ist_der_Button_gesperrt()
    {
        var freigabe = new TaskCompletionSource<LogleseErgebnis>();
        var a = await ErzeugeAsync(_ => freigabe.Task);
        a.Vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        a.Vm.ZeitraumBis = Jetzt.Date;

        var lauf = a.Vm.AnalyseStartenCommand.ExecuteAsync(null);

        Assert.True(a.Vm.IstAnalyseLaeuft);
        Assert.False(a.Vm.AnalyseStartenCommand.CanExecute(null));
        Assert.Contains("läuft", a.Vm.Statusmeldung);

        freigabe.SetResult(LogleseErgebnis.Leer);
        await lauf;
        Assert.False(a.Vm.IstAnalyseLaeuft);
    }

    /// <summary>Ein Fehler in der Analyse wird gemeldet statt die Anwendung zu beenden (NFA07).</summary>
    [Fact]
    public async Task Fehler_in_der_Analyse_wird_gemeldet()
    {
        var a = await ErzeugeAsync(_ => throw new InvalidOperationException("Lesefehler"));
        a.Vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        a.Vm.ZeitraumBis = Jetzt.Date;

        await a.Vm.AnalyseStartenCommand.ExecuteAsync(null);

        Assert.Contains("fehlgeschlagen", a.Vm.Statusmeldung);
        Assert.Contains("Lesefehler", a.Vm.Statusmeldung);
        Assert.False(a.Vm.IstAnalyseLaeuft);
    }

    /// <summary>Übersprungene Dateien erscheinen in der Statusleiste.</summary>
    [Fact]
    public async Task Uebersprungene_Dateien_in_der_Statusleiste()
    {
        var a = await ErzeugeAsync(_ => Task.FromResult(
            new LogleseErgebnis([], 3, 3 * 1024 * 1024, [new UebersprungeneDatei("x.log", "gesperrt")], ["1", "2", "3"])));
        a.Vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        a.Vm.ZeitraumBis = Jetzt.Date;

        await a.Vm.AnalyseStartenCommand.ExecuteAsync(null);

        Assert.Equal("Analyse abgeschlossen · 3 Dateien (3 MB) eingelesen · 1 übersprungen", a.Vm.Statusmeldung);
    }

    /// <summary>Ein Profilwechsel verwirft die Ergebnisse des vorherigen Profils.</summary>
    [Fact]
    public async Task Profilwechsel_verwirft_Ergebnisse()
    {
        var a = await ErzeugeAsync();
        a.Vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        a.Vm.ZeitraumBis = Jetzt.Date;
        await a.Vm.AnalyseStartenCommand.ExecuteAsync(null);

        a.Vm.AusgewaehltesProfil = Profil with { Name = "Anderes" };

        Assert.Equal("–", a.Dashboard.FehlerAnzahl);
        Assert.Empty(a.Logs.Eintraege);
    }

    /// <summary>„Bericht erstellen“ im Dashboard wechselt zur Ansicht „Bericht“.</summary>
    [Fact]
    public async Task Bericht_erstellen_wechselt_die_Ansicht()
    {
        var a = await ErzeugeAsync();
        a.Vm.ZeitraumVon = Jetzt.Date.AddDays(-6);
        a.Vm.ZeitraumBis = Jetzt.Date;
        await a.Vm.AnalyseStartenCommand.ExecuteAsync(null);

        a.Dashboard.BerichtErstellenCommand.Execute(null);

        Assert.IsType<BerichtViewModel>(a.Vm.AktuelleAnsicht);
    }
}
