using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests.Anwendung;

/// <summary>Tests des <see cref="ProfilService"/> (P1, GR02).</summary>
public class ProfilServiceTests
{
    private static Systemprofil Profil(string name, bool demo = false) => new()
    {
        Name = name,
        IstDemo = demo,
        Logquellen = [new Logquelle { Pfad = "x" }],
        Dienste = [new Dienst { Dienstname = "d" }],
    };

    private static (ProfilService Service, IProfilRepository Repo) Erzeuge(params Systemprofil[] vorhandene)
    {
        var repo = Substitute.For<IProfilRepository>();
        repo.LadenAlleAsync(Arg.Any<CancellationToken>()).Returns(vorhandene);
        return (new ProfilService(repo, new ProfilValidator(_ => true)), repo);
    }

    /// <summary>Beim ersten Start (keine Profile) wird das Demo-Profil gespeichert und aktiv (US02).</summary>
    [Fact]
    public async Task Erster_Start_legt_Demo_Profil_an()
    {
        var (service, repo) = Erzeuge();
        var demo = Profil("Demo", demo: true);
        repo.LadeDemoProfilAsync(Arg.Any<CancellationToken>()).Returns(demo);

        var start = await service.StartenAsync();

        Assert.True(start.DemoProfilAngelegt);
        Assert.Equal(demo, start.Aktiv);
        await repo.Received(1).SpeichernAsync(demo, null, Arg.Any<CancellationToken>());
    }

    /// <summary>Fehlt auch das Demo-Profil, startet die Anwendung ohne Profil.</summary>
    [Fact]
    public async Task Ohne_Profile_und_ohne_Demo()
    {
        var (service, _) = Erzeuge();

        var start = await service.StartenAsync();

        Assert.Empty(start.Profile);
        Assert.Null(start.Aktiv);
        Assert.False(start.DemoProfilAngelegt);
    }

    /// <summary>Vorhandene Profile: das zuletzt genutzte ist aktiv, ohne Merker das erste (alphabetisch).</summary>
    [Fact]
    public async Task Aktives_Profil_ist_das_zuletzt_genutzte()
    {
        var (service, repo) = Erzeuge(Profil("Zeta"), Profil("Alpha"));
        var ohneMerker = await service.StartenAsync();
        Assert.Equal("Alpha", ohneMerker.Aktiv?.Name);

        repo.LadeZuletztGenutztesAsync(Arg.Any<CancellationToken>()).Returns("zeta");
        var mitMerker = await service.StartenAsync();
        Assert.Equal("Zeta", mitMerker.Aktiv?.Name);
        await repo.DidNotReceive().SpeichernAsync(Arg.Any<Systemprofil>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Ein ungültiges Profil wird nicht gespeichert.</summary>
    [Fact]
    public async Task Ungueltiges_Profil_wird_nicht_gespeichert()
    {
        var (service, repo) = Erzeuge();

        var ergebnis = await service.SpeichernAsync(Profil("ab"), null);

        Assert.False(ergebnis.IstGueltig);
        await repo.DidNotReceive().SpeichernAsync(Arg.Any<Systemprofil>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Ein gültiges neues Profil wird gespeichert und erscheint in der Liste (US01).</summary>
    [Fact]
    public async Task Gueltiges_Profil_wird_gespeichert()
    {
        var (service, repo) = Erzeuge();
        await service.StartenAsync();
        var neu = Profil("Musterwerk");

        var ergebnis = await service.SpeichernAsync(neu, null);

        Assert.True(ergebnis.IstGueltig);
        await repo.Received(1).SpeichernAsync(neu, null, Arg.Any<CancellationToken>());
        Assert.Contains(neu, service.Profile);
    }

    /// <summary>Der eigene Name zählt beim Bearbeiten nicht als Duplikat, der Name eines anderen Profils schon.</summary>
    [Fact]
    public async Task Eindeutigkeit_gilt_nur_gegenueber_anderen_Profilen()
    {
        var (service, _) = Erzeuge(Profil("Eins"), Profil("Zwei"));
        await service.StartenAsync();

        Assert.True(service.Pruefen(Profil("Eins"), "Eins").IstGueltig);
        Assert.False(service.Pruefen(Profil("Zwei"), "Eins").IstGueltig);
        Assert.False(service.Pruefen(Profil("Eins"), null).IstGueltig);
    }

    /// <summary>Beim Umbenennen ersetzt das neue Profil das alte in der Liste.</summary>
    [Fact]
    public async Task Umbenennen_ersetzt_das_alte_Profil()
    {
        var (service, repo) = Erzeuge(Profil("Alt"));
        await service.StartenAsync();

        await service.SpeichernAsync(Profil("Neuer Name"), "Alt");

        Assert.Equal(["Neuer Name"], service.Profile.Select(p => p.Name));
        await repo.Received(1).SpeichernAsync(Arg.Is<Systemprofil>(p => p.Name == "Neuer Name"), "Alt", Arg.Any<CancellationToken>());
    }

    /// <summary>Demo-Profile bestehen die Prüfung auch ohne existierenden Logordner.</summary>
    [Fact]
    public void Demo_Profil_braucht_keinen_echten_Ordner()
    {
        var service = new ProfilService(Substitute.For<IProfilRepository>(), new ProfilValidator(_ => false));

        Assert.True(service.Pruefen(Profil("Demo", demo: true), null).IstGueltig);
        Assert.False(service.Pruefen(Profil("Echt"), null).IstGueltig);
    }
}
