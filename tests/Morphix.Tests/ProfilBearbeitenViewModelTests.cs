using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests;

/// <summary>Tests des Dialog-ViewModels „Profil bearbeiten“: Validierung am Feld, Speichern erst bei gültigen Eingaben.</summary>
public class ProfilBearbeitenViewModelTests
{
    private static (ProfilBearbeitenViewModel Vm, IProfilRepository Repo) Erzeuge(Systemprofil? profil = null, bool ordnerOk = true)
    {
        var repo = Substitute.For<IProfilRepository>();
        repo.LadenAlleAsync(Arg.Any<CancellationToken>()).Returns(new[] { new Systemprofil { Name = "Vorhanden" } });
        var service = new ProfilService(repo, new ProfilValidator(_ => ordnerOk));
        service.StartenAsync().GetAwaiter().GetResult();
        return (new ProfilBearbeitenViewModel(service, profil), repo);
    }

    /// <summary>Ein neues Profil startet ungültig, mit je einer leeren Zeile; Speichern ist gesperrt.</summary>
    [Fact]
    public void Neues_Profil_ist_zunaechst_ungueltig()
    {
        var (vm, _) = Erzeuge();

        Assert.Single(vm.Logquellen);
        Assert.Single(vm.Dienste);
        Assert.False(vm.IstGueltig);
        Assert.False(vm.SpeichernCommand.CanExecute(null));
        Assert.Equal("Neues Profil", vm.Titel);
        Assert.NotEmpty(vm.NameFehler);
        Assert.NotEmpty(vm.Logquellen[0].PfadFehler);
        Assert.NotEmpty(vm.Dienste[0].DienstnameFehler);
    }

    /// <summary>Nach korrekter Eingabe verschwinden die Fehler und Speichern wird frei.</summary>
    [Fact]
    public void Korrekte_Eingabe_aktiviert_Speichern()
    {
        var (vm, _) = Erzeuge();

        vm.Name = "Musterwerk";
        vm.Logquellen[0].Pfad = @"C:\Logs";
        vm.Dienste[0].Dienstname = "Muster-Server";

        Assert.True(vm.IstGueltig);
        Assert.True(vm.SpeichernCommand.CanExecute(null));
        Assert.Equal("", vm.NameFehler);
        Assert.Equal("", vm.Logquellen[0].PfadFehler);
        Assert.Equal("", vm.Dienste[0].DienstnameFehler);
    }

    /// <summary>Ein vorhandener Name wird sofort am Feld bemängelt, mit der Meldung der Spezifikation.</summary>
    [Fact]
    public void Doppelter_Name_wird_am_Feld_gemeldet()
    {
        var (vm, _) = Erzeuge();

        vm.Name = "vorhanden";

        Assert.Equal("Bitte einen eindeutigen Namen mit 3 bis 50 Zeichen eingeben.", vm.NameFehler);
    }

    /// <summary>Fehler werden der richtigen Zeile zugeordnet.</summary>
    [Fact]
    public void Fehler_gehoeren_zur_richtigen_Zeile()
    {
        var (vm, _) = Erzeuge(ordnerOk: true);
        vm.Name = "Musterwerk";
        vm.Dienste[0].Dienstname = "A";
        vm.Logquellen[0].Pfad = "x";

        vm.LogquelleHinzufuegenCommand.Execute(null);
        vm.Logquellen[1].Pfad = "y";
        vm.Logquellen[1].Dateimuster = "";

        Assert.Equal("", vm.Logquellen[0].DateimusterFehler);
        Assert.Equal("Bitte ein Dateimuster wie *.log angeben.", vm.Logquellen[1].DateimusterFehler);
        Assert.False(vm.IstGueltig);
    }

    /// <summary>Werden alle Logquellen entfernt, erscheint der Hinweis auf die Pflicht (GR02).</summary>
    [Fact]
    public void Ohne_Logquelle_und_Dienst_Fehler_an_der_Liste()
    {
        var (vm, _) = Erzeuge();

        vm.LogquelleEntfernenCommand.Execute(vm.Logquellen[0]);
        vm.DienstEntfernenCommand.Execute(vm.Dienste[0]);

        Assert.Equal("Bitte mindestens eine Logquelle angeben.", vm.LogquellenFehler);
        Assert.Equal("Bitte mindestens einen Dienst angeben.", vm.DiensteFehler);
    }

    /// <summary>Speichern übergibt das Profil an das Repository und schließt den Dialog mit Erfolg.</summary>
    [Fact]
    public async Task Speichern_schliesst_den_Dialog()
    {
        var (vm, repo) = Erzeuge();
        vm.Name = "  Musterwerk  ";
        vm.Logquellen[0].Pfad = @"C:\Logs";
        vm.Dienste[0].Dienstname = "Muster-Server";
        vm.AufgabeHinzufuegenCommand.Execute(null);
        vm.AufgabeHinzufuegenCommand.Execute(null);
        vm.Aufgaben[0].Name = "Export"; // die zweite, leere Aufgabe wird nicht übernommen
        bool? geschlossen = null;
        vm.SchliessenAngefordert += ok => geschlossen = ok;

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.True(geschlossen);
        Assert.Equal("Musterwerk", vm.Gespeichert?.Name);
        Assert.Equal(["Export"], vm.Gespeichert!.Aufgaben.Select(a => a.Name));
        await repo.Received(1).SpeichernAsync(Arg.Is<Systemprofil>(p => p.Name == "Musterwerk"), null, Arg.Any<CancellationToken>());
    }

    /// <summary>Scheitert das Schreiben, bleibt der Dialog offen und zeigt den Grund.</summary>
    [Fact]
    public async Task Schreibfehler_bleibt_im_Dialog()
    {
        var (vm, repo) = Erzeuge();
        repo.SpeichernAsync(Arg.Any<Systemprofil>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new IOException("Datenträger voll"));
        vm.Name = "Musterwerk";
        vm.Logquellen[0].Pfad = "x";
        vm.Dienste[0].Dienstname = "A";
        bool? geschlossen = null;
        vm.SchliessenAngefordert += ok => geschlossen = ok;

        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.Null(geschlossen);
        Assert.Contains("Datenträger voll", vm.SpeicherFehler);
    }

    /// <summary>Beim Bearbeiten bleiben nicht bearbeitbare Werte (Status, Zeilenmuster) erhalten.</summary>
    [Fact]
    public async Task Bearbeiten_erhaelt_unsichtbare_Werte()
    {
        var profil = new Systemprofil
        {
            Name = "Demo-Profil",
            IstDemo = true,
            Logquellen = [new Logquelle { Pfad = "logs", Zeilenmuster = "(?<zeit>a)(?<stufe>b)(?<text>c)" }],
            Dienste = [new Dienst { Dienstname = "D", Status = DienstStatus.Gestoppt, IstKritisch = true }],
        };
        var (vm, _) = Erzeuge(profil);

        vm.Beschreibung = "neu";
        await vm.SpeichernCommand.ExecuteAsync(null);

        Assert.Equal("Profil bearbeiten", vm.Titel);
        Assert.True(vm.Gespeichert!.IstDemo);
        Assert.Equal("(?<zeit>a)(?<stufe>b)(?<text>c)", vm.Gespeichert.Logquellen[0].Zeilenmuster);
        Assert.Equal(DienstStatus.Gestoppt, vm.Gespeichert.Dienste[0].Status);
    }

    /// <summary>„Abbrechen“ schließt den Dialog ohne Speichern.</summary>
    [Fact]
    public void Abbrechen_schliesst_ohne_Speichern()
    {
        var (vm, repo) = Erzeuge();
        bool? geschlossen = null;
        vm.SchliessenAngefordert += ok => geschlossen = ok;

        vm.AbbrechenCommand.Execute(null);

        Assert.False(geschlossen);
        Assert.Null(vm.Gespeichert);
        repo.DidNotReceive().SpeichernAsync(Arg.Any<Systemprofil>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
