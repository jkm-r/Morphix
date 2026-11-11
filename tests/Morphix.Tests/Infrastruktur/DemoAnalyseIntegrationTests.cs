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

/// <summary>
/// Integrationstest für das Fertig-Kriterium von M6: Im Demo-Modus lässt sich ein Bericht erzeugen und mit
/// den echten Adaptern als ZIP exportieren; mit Maskierung enthält das ZIP keine IP-Adressen mehr.
/// </summary>
public sealed class DemoBerichtIntegrationTests : IDisposable
{
    private static readonly string DemoOrdner = System.IO.Path.Combine(AppContext.BaseDirectory, "demo");
    private readonly string _ziel = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "morphix-bericht-" + Guid.NewGuid().ToString("N") + ".zip");

    public void Dispose()
    {
        if (System.IO.File.Exists(_ziel))
        {
            System.IO.File.Delete(_ziel);
        }
    }

    private static async Task<Diagnose> DemoDiagnoseAsync()
    {
        var repo = new JsonProfilRepository("x", "y", System.IO.Path.Combine(DemoOrdner, "demo-profil.json"));
        var profil = (await repo.LadeDemoProfilAsync())!;
        var service = new DiagnoseService(new WindowsDatenquellenFabrik(DemoOrdner));
        var (von, bis, _) = service.PruefeTage(DateTime.Today.AddDays(-6), DateTime.Today);
        return await service.ErstellenAsync(profil, von, bis);
    }

    private static string[] LeseAlleEintraege(string zipPfad)
    {
        using var zip = System.IO.Compression.ZipFile.OpenRead(zipPfad);
        return [.. zip.Entries.Select(e =>
        {
            using var leser = new System.IO.StreamReader(e.Open());
            return e.FullName + "\n" + leser.ReadToEnd();
        })];
    }

    /// <summary>Bericht und Logs landen im ZIP; mit Maskierung steht keine IPv4-Adresse mehr darin, die Demo-Originale bleiben unverändert.</summary>
    [Fact]
    public async Task Demo_Bericht_wird_maskiert_als_ZIP_exportiert()
    {
        var diagnose = await DemoDiagnoseAsync();
        var originale = diagnose.GeleseneDateien.ToDictionary(p => p, p => System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(p)));
        var service = new BerichtService(new Morphix.Infrastructure.Berichte.HtmlZipExporter());

        var bericht = await service.ExportierenAsync(diagnose, maskieren: true, _ziel);

        Assert.Equal(_ziel, bericht.Dateipfad);
        var eintraege = LeseAlleEintraege(_ziel);
        Assert.Equal(4, eintraege.Length);
        Assert.Contains(eintraege, e => e.StartsWith("bericht.html"));
        Assert.Equal(3, eintraege.Count(e => e.StartsWith("logs/")));
        var ipv4 = new System.Text.RegularExpressions.Regex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b");
        Assert.All(eintraege, e => Assert.DoesNotMatch(ipv4, e));
        Assert.Contains(eintraege, e => e.StartsWith("logs/") && e.Contains(DatenMaskierer.IpErsatz));

        foreach (var (pfad, hash) in originale)
        {
            Assert.Equal(hash, System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(pfad)));
        }
    }
}
