using Morphix.Core.Domaene;
using Morphix.Infrastructure.Profile;

namespace Morphix.Tests.Infrastruktur;

/// <summary>Tests des <see cref="JsonProfilRepository"/> mit einem temporären Ordner.</summary>
public sealed class JsonProfilRepositoryTests : IDisposable
{
    private readonly string _wurzel = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "morphix-test-" + Guid.NewGuid().ToString("N"));

    private JsonProfilRepository Repository(string? demoDatei = null) => new(
        System.IO.Path.Combine(_wurzel, "Profile"),
        System.IO.Path.Combine(_wurzel, "einstellungen.json"),
        demoDatei ?? System.IO.Path.Combine(AppContext.BaseDirectory, "demo", "demo-profil.json"));

    /// <summary>Räumt den temporären Ordner auf.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_wurzel))
        {
            Directory.Delete(_wurzel, recursive: true);
        }
    }

    private static Systemprofil Profil(string name) => new()
    {
        Name = name,
        Beschreibung = "Test äöü",
        Logquellen = [new Logquelle { Pfad = @"C:\Logs", Dateimuster = "*.txt", Zeilenmuster = @"^(?<zeit>x) (?<stufe>y) (?<text>z)$" }],
        Dienste = [new Dienst { Dienstname = "ZE-Server", Anzeigename = "Server", IstKritisch = true }],
        Aufgaben = [new GeplanteAufgabe { Name = "Export" }],
    };

    /// <summary>Ein gespeichertes Profil wird unverändert wieder geladen (US01).</summary>
    [Fact]
    public async Task Speichern_und_Laden_ergibt_dasselbe_Profil()
    {
        var repo = Repository();
        var profil = Profil("Musterwerk");

        await repo.SpeichernAsync(profil);
        var geladen = Assert.Single(await repo.LadenAlleAsync());

        Assert.Equal(profil.Name, geladen.Name);
        Assert.Equal(profil.Beschreibung, geladen.Beschreibung);
        Assert.Equal(profil.Logquellen, geladen.Logquellen);
        Assert.Equal(profil.Dienste, geladen.Dienste);
        Assert.Equal(profil.Aufgaben, geladen.Aufgaben);
    }

    /// <summary>Ohne Ordner ist die Liste leer, ohne dass etwas angelegt wird.</summary>
    [Fact]
    public async Task Ohne_Ordner_keine_Profile()
    {
        Assert.Empty(await Repository().LadenAlleAsync());
        Assert.False(Directory.Exists(_wurzel));
    }

    /// <summary>Beim Umbenennen verschwindet die alte Datei.</summary>
    [Fact]
    public async Task Umbenennen_entfernt_alte_Datei()
    {
        var repo = Repository();
        await repo.SpeichernAsync(Profil("Alt"));

        await repo.SpeichernAsync(Profil("Neu"), "Alt");

        Assert.Equal(["Neu"], (await repo.LadenAlleAsync()).Select(p => p.Name));
    }

    /// <summary>Namen mit ungültigen Dateizeichen funktionieren und kollidieren nicht.</summary>
    [Fact]
    public async Task Sonderzeichen_im_Namen()
    {
        var repo = Repository();

        await repo.SpeichernAsync(Profil("A/B"));
        await repo.SpeichernAsync(Profil("A?B"));

        var namen = (await repo.LadenAlleAsync()).Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["A/B", "A?B"], namen);
    }

    /// <summary>Eine defekte Profildatei wird übersprungen, die anderen bleiben nutzbar (NFA07).</summary>
    [Fact]
    public async Task Defekte_Datei_wird_uebersprungen()
    {
        var repo = Repository();
        await repo.SpeichernAsync(Profil("Gut"));
        await File.WriteAllTextAsync(System.IO.Path.Combine(_wurzel, "Profile", "kaputt.json"), "{ das ist kein json");

        var profile = await repo.LadenAlleAsync();

        Assert.Equal(["Gut"], profile.Select(p => p.Name));
    }

    /// <summary>Das Löschen entfernt das Profil.</summary>
    [Fact]
    public async Task Loeschen()
    {
        var repo = Repository();
        await repo.SpeichernAsync(Profil("Weg"));

        await repo.LoeschenAsync("Weg");

        Assert.Empty(await repo.LadenAlleAsync());
    }

    /// <summary>Das zuletzt genutzte Profil wird gemerkt; ohne Datei ist es <c>null</c>.</summary>
    [Fact]
    public async Task Zuletzt_genutztes_Profil()
    {
        var repo = Repository();
        Assert.Null(await repo.LadeZuletztGenutztesAsync());

        await repo.MerkeZuletztGenutztesAsync("Musterwerk");

        Assert.Equal("Musterwerk", await repo.LadeZuletztGenutztesAsync());
    }

    /// <summary>Das mitgelieferte Demo-Profil ist gültig und enthält die geplanten Elemente (US02).</summary>
    [Fact]
    public async Task Demo_Profil_wird_geladen()
    {
        var demo = await Repository().LadeDemoProfilAsync();

        Assert.NotNull(demo);
        Assert.True(demo.IstDemo);
        Assert.Equal("Demo – Zeiterfassung Musterwerk", demo.Name);
        Assert.Equal(4, demo.Dienste.Count);
        Assert.Equal(2, demo.Dienste.Count(d => d.IstKritisch));
        Assert.Single(demo.Dienste, d => d.Status == DienstStatus.Gestoppt);
        Assert.Equal(3, demo.Aufgaben.Count);
        Assert.True(new ProfilValidator().Pruefen(demo, []).IstGueltig);
    }

    /// <summary>Fehlt die Demo-Datei, ist das Ergebnis <c>null</c>.</summary>
    [Fact]
    public async Task Fehlendes_Demo_Profil()
    {
        Assert.Null(await Repository(System.IO.Path.Combine(_wurzel, "gibt-es-nicht.json")).LadeDemoProfilAsync());
    }
}
