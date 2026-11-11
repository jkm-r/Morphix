using Morphix.App.ViewModels;

namespace Morphix.Tests;

/// <summary>Tests der Navigation im <see cref="MainViewModel"/>.</summary>
public class MainViewModelTests
{
    private static MainViewModel Erzeuge() =>
        new(new DashboardViewModel(), new LogsUndFehlerViewModel(), new DiensteViewModel(),
            new AufgabenViewModel(), new BerichtViewModel());

    /// <summary>Beim Start ist das Dashboard sichtbar.</summary>
    [Fact]
    public void Start_zeigt_Dashboard()
    {
        var vm = Erzeuge();

        Assert.IsType<DashboardViewModel>(vm.AktuelleAnsicht);
    }

    /// <summary>Die Auswahl eines Navigationseintrags wechselt die aktuelle Ansicht.</summary>
    [Theory]
    [InlineData(1, typeof(LogsUndFehlerViewModel))]
    [InlineData(2, typeof(DiensteViewModel))]
    [InlineData(3, typeof(AufgabenViewModel))]
    [InlineData(4, typeof(BerichtViewModel))]
    public void Auswahl_wechselt_Ansicht(int index, Type erwartet)
    {
        var vm = Erzeuge();

        vm.AusgewaehlterEintrag = vm.Navigation[index];

        Assert.IsType(erwartet, vm.AktuelleAnsicht);
    }

    /// <summary>Die Navigation enthält die fünf Ansichten aus der Spezifikation.</summary>
    [Fact]
    public void Navigation_enthaelt_fuenf_Ansichten()
    {
        var titel = Erzeuge().Navigation.Select(e => e.Titel);

        Assert.Equal(["Dashboard", "Logs und Fehler", "Dienste", "Aufgaben", "Bericht"], titel);
    }

    /// <summary>Der DI-Container kann das Hauptfenster-ViewModel auflösen.</summary>
    [Fact]
    public void Container_loest_MainViewModel_auf()
    {
        using var container = Morphix.App.App.BaueContainer();

        Assert.NotNull(container.GetService(typeof(MainViewModel)));
    }
}
