using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Infrastructure;
using Morphix.Infrastructure.Profile;

namespace Morphix.Tests.Infrastruktur;

/// <summary>
/// Integrationstest für das Fertig-Kriterium von M4: Im Demo-Modus liefert die Analyse mit den echten
/// Demo-Adaptern ein gefülltes Dashboard und filterbare Fehlergruppen.
/// </summary>
public class DemoAnalyseIntegrationTests
{
    private static readonly string DemoOrdner = System.IO.Path.Combine(AppContext.BaseDirectory, "demo");

    /// <summary>Standardzeitraum (7 Tage) mit dem Demo-Profil.</summary>
    [Fact]
    public async Task Demo_Analyse_fuellt_Dashboard_und_Logansicht()
    {
        var repo = new JsonProfilRepository("x", "y", System.IO.Path.Combine(DemoOrdner, "demo-profil.json"));
        var profil = (await repo.LadeDemoProfilAsync())!;
        var service = new DiagnoseService(new WindowsDatenquellenFabrik(DemoOrdner));
        var (von, bis, pruefung) = service.PruefeTage(DateTime.Today.AddDays(-6), DateTime.Today);
        Assert.True(pruefung.IstGueltig);

        var diagnose = await service.ErstellenAsync(profil, von, bis);

        Assert.True(diagnose.IstAbgeschlossen);
        Assert.Equal(3, diagnose.GeleseneDateien.Count);
        Assert.NotEmpty(diagnose.Logeintraege);
        Assert.NotEmpty(diagnose.Fehlergruppen);
        Assert.Equal(4, diagnose.Dienste.Count);
        Assert.Equal(3, diagnose.Aufgaben.Count);

        var dashboard = new DashboardViewModel();
        dashboard.Zeige(diagnose);
        Assert.NotEqual("–", dashboard.FehlerAnzahl);
        Assert.Equal("1", dashboard.GestoppteKritischeDienste);
        Assert.Equal("1", dashboard.FehlgeschlageneAufgaben);
        Assert.Equal(5, dashboard.HaeufigsteGruppen.Count);

        var logs = new LogsUndFehlerViewModel();
        logs.Zeige(diagnose);
        var alle = logs.Gruppen.Count;
        logs.Suchbegriff = "Datenbankverbindung";
        Assert.True(logs.Gruppen.Count is > 0 && logs.Gruppen.Count < alle);
        logs.GruppeOeffnenCommand.Execute(logs.Gruppen[0]);
        Assert.Equal(logs.Gruppen[0].Anzahl, logs.Eintraege.Count);
    }
}
