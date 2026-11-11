using Morphix.App.Dialoge;
using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests;

/// <summary>
/// Tests des <see cref="DiensteViewModel"/> mit Fakes (US07, US08, US09, GR03). Echte Dienste werden nie gesteuert.
/// </summary>
public class DiensteViewModelTests
{
    private static readonly Systemprofil Profil = new()
    {
        Name = "Test",
        Logquellen = [new Logquelle { Pfad = "x" }],
        Dienste =
        [
            new Dienst { Dienstname = "A", IstKritisch = true },
            new Dienst { Dienstname = "B" },
            new Dienst { Dienstname = "C", IstKritisch = true },
        ],
    };

    private sealed record Aufbau(
        DiensteViewModel Vm, IDienstGateway Gateway, IRechtePruefer Rechte, IBestaetigungsDialog Dialog, Dictionary<string, DienstStatus> Zustand);

    private static Aufbau Erzeuge(bool admin = true, bool bestaetigen = true, TimeSpan? intervall = null)
    {
        var zustand = new Dictionary<string, DienstStatus>
        {
            ["A"] = DienstStatus.Gestoppt,
            ["B"] = DienstStatus.Gestartet,
            ["C"] = DienstStatus.NichtGefunden,
        };
        var gateway = Substitute.For<IDienstGateway>();
        gateway.AbfragenAsync(Arg.Any<IEnumerable<Dienst>>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult<IReadOnlyList<Dienst>>(
                [.. call.Arg<IEnumerable<Dienst>>().Select(d => d with { Status = zustand[d.Dienstname] })]));
        gateway.AusfuehrenAsync(Arg.Any<Dienst>(), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var d = call.Arg<Dienst>();
                zustand[d.Dienstname] = call.Arg<DienstAktionsart>() == DienstAktionsart.Stoppen ? DienstStatus.Gestoppt : DienstStatus.Gestartet;
                return Task.FromResult(d with { Status = zustand[d.Dienstname] });
            });
        var rechte = Substitute.For<IRechtePruefer>();
        rechte.HatAdminrechte().Returns(admin);
        var dialog = Substitute.For<IBestaetigungsDialog>();
        dialog.Bestaetigen(Arg.Any<string>(), Arg.Any<string>()).Returns(bestaetigen);

        var vm = new DiensteViewModel(
            new DienstService(TestAufbau.FakeFabrik(new Datenquellen(
                Substitute.For<ILogLeser>(), gateway, Substitute.For<IAufgabenGateway>(), Substitute.For<IEreignisLeser>(), rechte))),
            dialog,
            intervall);
        return new Aufbau(vm, gateway, rechte, dialog, zustand);
    }

    private static async Task<Aufbau> GeladenAsync(bool admin = true, bool bestaetigen = true)
    {
        var a = Erzeuge(admin, bestaetigen);
        a.Vm.SetzeProfil(Profil);
        await a.Vm.AktualisierenAsync();
        return a;
    }

    private static void Waehle(Aufbau a, string name) =>
        a.Vm.AusgewaehlterDienst = a.Vm.Dienste.First(z => z.Dienstname == name);

    /// <summary>Die Tabelle zeigt deutsche Statustexte, kritische gestoppte Dienste sind hervorgehoben (US09).</summary>
    [Fact]
    public async Task Zeigt_Status_und_hebt_kritische_gestoppte_Dienste_hervor()
    {
        var a = await GeladenAsync();

        Assert.Equal(["Gestoppt", "Gestartet", "Nicht gefunden"], a.Vm.Dienste.Select(z => z.StatusText));
        Assert.True(a.Vm.Dienste[0].IstKritischGestoppt);
        Assert.False(a.Vm.Dienste[1].IstKritischGestoppt);
        Assert.False(a.Vm.Dienste[2].IstKritischGestoppt);
        Assert.Equal("Achtung: Der kritische Dienst A läuft nicht.", a.Vm.KritischHinweis);
        Assert.Equal("Dieser Dienst ist auf dem System nicht vorhanden.", a.Vm.Dienste[2].Hinweis);
    }

    /// <summary>Ohne gestoppte kritische Dienste gibt es keinen Warnhinweis; bei mehreren werden alle genannt.</summary>
    [Fact]
    public async Task Kritisch_Hinweis_Varianten()
    {
        var a = await GeladenAsync();

        a.Zustand["A"] = DienstStatus.Gestartet;
        await a.Vm.AktualisierenAsync();
        Assert.Equal("", a.Vm.KritischHinweis);

        a.Zustand["A"] = DienstStatus.Gestoppt;
        a.Zustand["C"] = DienstStatus.Gestoppt;
        await a.Vm.AktualisierenAsync();
        Assert.Equal("Achtung: 2 kritische Dienste laufen nicht: A, C.", a.Vm.KritischHinweis);
    }

    /// <summary>Welche Schaltfläche möglich ist, hängt vom Status des gewählten Dienstes ab.</summary>
    [Fact]
    public async Task Schaltflaechen_je_nach_Status()
    {
        var a = await GeladenAsync();

        Waehle(a, "A"); // gestoppt
        Assert.True(a.Vm.StartenCommand.CanExecute(null));
        Assert.False(a.Vm.StoppenCommand.CanExecute(null));
        Assert.False(a.Vm.NeustartenCommand.CanExecute(null));

        Waehle(a, "B"); // gestartet
        Assert.False(a.Vm.StartenCommand.CanExecute(null));
        Assert.True(a.Vm.StoppenCommand.CanExecute(null));
        Assert.True(a.Vm.NeustartenCommand.CanExecute(null));

        Waehle(a, "C"); // nicht gefunden
        Assert.False(a.Vm.StartenCommand.CanExecute(null));
        Assert.False(a.Vm.StoppenCommand.CanExecute(null));
        Assert.False(a.Vm.NeustartenCommand.CanExecute(null));
    }

    /// <summary>US08: Ohne Administratorrechte bleibt die Ansicht lesend, mit Hinweistext.</summary>
    [Fact]
    public async Task Ohne_Adminrechte_sind_alle_Schaltflaechen_gesperrt()
    {
        var a = await GeladenAsync(admin: false);
        Waehle(a, "B");

        Assert.False(a.Vm.HatAdminrechte);
        Assert.False(a.Vm.StartenCommand.CanExecute(null));
        Assert.False(a.Vm.StoppenCommand.CanExecute(null));
        Assert.False(a.Vm.NeustartenCommand.CanExecute(null));
        Assert.Contains("nur lesend", a.Vm.RechteHinweis);
        Assert.Equal(3, a.Vm.Dienste.Count); // Anzeige bleibt möglich
    }

    /// <summary>Ohne ausgewählte Zeile ist nichts möglich.</summary>
    [Fact]
    public async Task Ohne_Auswahl_keine_Aktion()
    {
        var a = await GeladenAsync();

        Assert.False(a.Vm.StoppenCommand.CanExecute(null));
    }

    /// <summary>US07: Nach Bestätigung wird der Dienst gestoppt, die Frage nennt den Dienst; die Liste zeigt den neuen Zustand.</summary>
    [Fact]
    public async Task Stoppen_nach_Bestaetigung()
    {
        var a = await GeladenAsync();
        Waehle(a, "B");

        await a.Vm.StoppenCommand.ExecuteAsync(null);

        a.Dialog.Received(1).Bestaetigen("Dienstaktion bestätigen", "Dienst B wirklich stoppen?");
        await a.Gateway.Received(1).AusfuehrenAsync(Arg.Is<Dienst>(d => d.Dienstname == "B"), DienstAktionsart.Stoppen, Arg.Any<CancellationToken>());
        Assert.Equal("Dienst „B“ wurde gestoppt.", a.Vm.Meldung);
        Assert.Equal("Gestoppt", a.Vm.Dienste.First(z => z.Dienstname == "B").StatusText);
        Assert.False(a.Vm.IstBeschaeftigt);
    }

    /// <summary>Die Fragetexte für Starten und Neustarten.</summary>
    [Fact]
    public async Task Fragetexte()
    {
        var a = await GeladenAsync();

        Waehle(a, "A");
        await a.Vm.StartenCommand.ExecuteAsync(null);
        Waehle(a, "B");
        await a.Vm.NeustartenCommand.ExecuteAsync(null);

        a.Dialog.Received(1).Bestaetigen(Arg.Any<string>(), "Dienst A wirklich starten?");
        a.Dialog.Received(1).Bestaetigen(Arg.Any<string>(), "Dienst B wirklich neu starten?");
    }

    /// <summary>GR03: Bricht der Anwender ab, wird nichts ausgeführt.</summary>
    [Fact]
    public async Task Abbrechen_fuehrt_nichts_aus()
    {
        var a = await GeladenAsync(bestaetigen: false);
        Waehle(a, "B");

        await a.Vm.StoppenCommand.ExecuteAsync(null);

        await a.Gateway.DidNotReceiveWithAnyArgs().AusfuehrenAsync(default!, default, default);
        Assert.Equal("Die Aktion wurde abgebrochen.", a.Vm.Meldung);
        Assert.Equal("Gestartet", a.Vm.Dienste.First(z => z.Dienstname == "B").StatusText);
    }

    /// <summary>Scheitert die Aktion, erscheint die Meldung und die Liste bleibt aktuell.</summary>
    [Fact]
    public async Task Fehlgeschlagene_Aktion_zeigt_Meldung()
    {
        var a = await GeladenAsync();
        a.Gateway.AusfuehrenAsync(Arg.Any<Dienst>(), Arg.Any<DienstAktionsart>(), Arg.Any<CancellationToken>())
            .Returns<Task<Dienst>>(_ => throw new DienstAktionFehlgeschlagenException("Zugriff verweigert."));
        Waehle(a, "B");

        await a.Vm.StoppenCommand.ExecuteAsync(null);

        Assert.Equal("Zugriff verweigert.", a.Vm.Meldung);
        Assert.False(a.Vm.IstBeschaeftigt);
    }

    /// <summary>Ein unerwarteter Fehler beim Abfragen wird gemeldet statt die Anwendung zu beenden.</summary>
    [Fact]
    public async Task Abfragefehler_wird_gemeldet()
    {
        var a = Erzeuge();
        a.Gateway.AbfragenAsync(Arg.Any<IEnumerable<Dienst>>(), Arg.Any<CancellationToken>())
            .Returns<Task<IReadOnlyList<Dienst>>>(_ => throw new InvalidOperationException("Dienstverwaltung nicht erreichbar"));
        a.Vm.SetzeProfil(Profil);

        await a.Vm.AktualisierenAsync();

        Assert.Contains("nicht erreichbar", a.Vm.Meldung);
    }

    /// <summary>Beim Aktualisieren bleibt die gewählte Zeile ausgewählt.</summary>
    [Fact]
    public async Task Auswahl_bleibt_beim_Aktualisieren()
    {
        var a = await GeladenAsync();
        Waehle(a, "B");

        a.Zustand["B"] = DienstStatus.Gestoppt;
        await a.Vm.AktualisierenAsync();

        Assert.Equal("B", a.Vm.AusgewaehlterDienst?.Dienstname);
        Assert.Equal(DienstStatus.Gestoppt, a.Vm.AusgewaehlterDienst?.Dienst.Status);
    }

    /// <summary>Ohne Profil gibt es nichts zu tun; ein Profilwechsel leert die Liste.</summary>
    [Fact]
    public async Task Profilwechsel_leert_die_Liste()
    {
        var a = await GeladenAsync();
        Assert.Equal("", a.Vm.LeerHinweis);

        a.Vm.SetzeProfil(null);

        Assert.Empty(a.Vm.Dienste);
        Assert.Equal("Wähle zuerst ein Profil.", a.Vm.LeerHinweis);
        Assert.False(a.Vm.HatAdminrechte);
        await a.Vm.AktualisierenAsync(); // darf ohne Profil nichts tun
    }

    /// <summary>P4: Solange die Ansicht aktiv ist, wird regelmäßig neu abgefragt; danach nicht mehr.</summary>
    [Fact]
    public async Task Aktualisiert_regelmaessig_solange_aktiv()
    {
        var a = Erzeuge(intervall: TimeSpan.FromMilliseconds(30));
        a.Vm.SetzeProfil(Profil);

        a.Vm.IstAktiv = true;
        await Warte(() => a.Gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IDienstGateway.AbfragenAsync)) >= 4);
        Assert.Equal(3, a.Vm.Dienste.Count);

        a.Vm.IstAktiv = false;
        await Task.Delay(50);
        var anzahl = a.Gateway.ReceivedCalls().Count();
        await Task.Delay(150);
        Assert.Equal(anzahl, a.Gateway.ReceivedCalls().Count());
        a.Vm.Dispose();
    }

    /// <summary>Das Standardintervall beträgt laut Spezifikation 5 Sekunden.</summary>
    [Fact]
    public void Standardintervall_ist_5_Sekunden()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), DiensteViewModel.StandardIntervall);
    }

    private static async Task Warte(Func<bool> bedingung)
    {
        for (var i = 0; i < 100 && !bedingung(); i++)
        {
            await Task.Delay(20);
        }

        Assert.True(bedingung(), "Die Bedingung wurde nicht rechtzeitig erfüllt.");
    }
}
