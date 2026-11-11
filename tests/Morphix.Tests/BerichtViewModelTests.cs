using Morphix.App.Dialoge;
using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests;

/// <summary>Tests des <see cref="BerichtViewModel"/> mit Fake-Exporter und Fake-Dialogen.</summary>
public class BerichtViewModelTests
{
    private static readonly DateTime T0 = new(2026, 11, 10, 8, 0, 0);

    private sealed record Aufbau(BerichtViewModel Vm, IBerichtExporter Exporter, IExportDialog Dialog, IBestaetigungsDialog Bestaetigung);

    private static Aufbau Erzeuge(bool bestaetigt = true, bool beschreibbar = true, string? zielpfad = "C:\\Ziel\\bericht.zip")
    {
        var exporter = Substitute.For<IBerichtExporter>();
        exporter.KannSchreiben(Arg.Any<string>()).Returns(beschreibbar);
        var dialog = Substitute.For<IExportDialog>();
        dialog.ZielWaehlen(Arg.Any<string>()).Returns(zielpfad);
        var bestaetigung = Substitute.For<IBestaetigungsDialog>();
        bestaetigung.Bestaetigen(Arg.Any<string>(), Arg.Any<string>()).Returns(bestaetigt);
        return new Aufbau(TestAufbau.Bericht(exporter, dialog, bestaetigung), exporter, dialog, bestaetigung);
    }

    private static Diagnose Diagnose(bool abgeschlossen = true, string profil = "P") => new()
    {
        Profil = new Systemprofil { Name = profil },
        Von = T0,
        Bis = T0.AddDays(1),
        Logeintraege = [new Logeintrag(T0, Schweregrad.Fehler, "a.log", "Fehler von 10.1.2.3")],
        Fehlergruppen = FehlerGruppierer.Gruppiere([new Logeintrag(T0, Schweregrad.Fehler, "a.log", "Fehler von 10.1.2.3")]),
        GeleseneDateien = ["C:\\Logs\\a.log"],
        IstAbgeschlossen = abgeschlossen,
    };

    private static async Task<BerichtViewModel> MitDiagnose(Aufbau a, Diagnose? d = null)
    {
        a.Vm.Zeige(d ?? Diagnose());
        await a.Vm.VorschauFertigAsync();
        return a.Vm;
    }

    /// <summary>Ohne Diagnose gibt es keine Vorschau, keinen Export und einen Hinweis (GR09).</summary>
    [Fact]
    public void Ohne_Diagnose()
    {
        var a = Erzeuge();

        Assert.False(a.Vm.HatDiagnose);
        Assert.Equal("", a.Vm.Vorschau);
        Assert.False(a.Vm.ExportierenCommand.CanExecute(null));
        Assert.NotEqual("", a.Vm.LeerHinweis);
    }

    /// <summary>Eine nicht abgeschlossene Diagnose wird nicht übernommen (GR09).</summary>
    [Fact]
    public async Task Offene_Diagnose_wird_nicht_uebernommen()
    {
        var a = Erzeuge();

        await MitDiagnose(a, Diagnose(abgeschlossen: false));

        Assert.False(a.Vm.HatDiagnose);
        Assert.False(a.Vm.ExportierenCommand.CanExecute(null));
    }

    /// <summary>Mit Diagnose erscheint die Vorschau; die Maskierung ist standardmäßig an (GR10).</summary>
    [Fact]
    public async Task Vorschau_ist_standardmaessig_maskiert()
    {
        var a = Erzeuge();

        await MitDiagnose(a);

        Assert.True(a.Vm.HatDiagnose);
        Assert.True(a.Vm.Maskieren);
        Assert.StartsWith("<!DOCTYPE html>", a.Vm.Vorschau);
        Assert.DoesNotContain("10.1.2.3", a.Vm.Vorschau);
        Assert.Equal("", a.Vm.MaskierungHinweis);
        Assert.True(a.Vm.ExportierenCommand.CanExecute(null));
    }

    /// <summary>Abschalten nach Bestätigung: Die Vorschau zeigt Klartext und der Hinweis warnt.</summary>
    [Fact]
    public async Task Maskierung_nach_Bestaetigung_abschaltbar()
    {
        var a = Erzeuge(bestaetigt: true);
        await MitDiagnose(a);

        a.Vm.Maskieren = false;
        await a.Vm.VorschauFertigAsync();

        Assert.False(a.Vm.Maskieren);
        Assert.Contains("10.1.2.3", a.Vm.Vorschau);
        Assert.NotEqual("", a.Vm.MaskierungHinweis);
        a.Bestaetigung.Received(1).Bestaetigen(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>GR10: „Bewusst“ abschalten heißt mit Bestätigung; ohne sie bleibt die Maskierung an.</summary>
    [Fact]
    public async Task Maskierung_bleibt_an_ohne_Bestaetigung()
    {
        var a = Erzeuge(bestaetigt: false);
        await MitDiagnose(a);

        a.Vm.Maskieren = false;
        await a.Vm.VorschauFertigAsync();

        Assert.True(a.Vm.Maskieren);
        Assert.DoesNotContain("10.1.2.3", a.Vm.Vorschau);
    }

    /// <summary>GR10: Die Abschaltung gilt pro Bericht; eine neue Analyse schaltet die Maskierung wieder ein.</summary>
    [Fact]
    public async Task Neue_Diagnose_schaltet_Maskierung_wieder_ein()
    {
        var a = Erzeuge();
        await MitDiagnose(a);
        a.Vm.Maskieren = false;

        await MitDiagnose(a);

        Assert.True(a.Vm.Maskieren);
        Assert.DoesNotContain("10.1.2.3", a.Vm.Vorschau);
        a.Bestaetigung.Received(1).Bestaetigen(Arg.Any<string>(), Arg.Any<string>());
    }

    /// <summary>Zurücksetzen (z. B. Profilwechsel) leert Vorschau und Meldung.</summary>
    [Fact]
    public async Task Zeige_null_setzt_zurueck()
    {
        var a = Erzeuge();
        await MitDiagnose(a);

        a.Vm.Zeige(null);
        await a.Vm.VorschauFertigAsync();

        Assert.False(a.Vm.HatDiagnose);
        Assert.Equal("", a.Vm.Vorschau);
        Assert.Equal("", a.Vm.Meldung);
    }

    /// <summary>Export: Der Dialog bekommt den Dateinamenvorschlag, der Service schreibt, die Meldung nennt den Pfad.</summary>
    [Fact]
    public async Task Export_schreibt_und_meldet_Pfad()
    {
        var a = Erzeuge();
        await MitDiagnose(a);

        await a.Vm.ExportierenCommand.ExecuteAsync(null);

        a.Dialog.Received(1).ZielWaehlen(Arg.Is<string>(n => n.StartsWith("Morphix_P_") && n.EndsWith(".zip")));
        await a.Exporter.Received(1).ExportierenAsync(
            Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), "C:\\Ziel\\bericht.zip",
            Arg.Is<Func<string, string>?>(f => f != null), Arg.Any<CancellationToken>());
        Assert.Contains("C:\\Ziel\\bericht.zip", a.Vm.Meldung);
        Assert.Contains("maskiert", a.Vm.Meldung);
        Assert.False(a.Vm.MeldungIstFehler);
        Assert.False(a.Vm.IstExportLaeuft);
    }

    /// <summary>Bricht der Anwender den Dialog ab, passiert nichts.</summary>
    [Fact]
    public async Task Export_abgebrochen_tut_nichts()
    {
        var a = Erzeuge(zielpfad: null);
        await MitDiagnose(a);

        await a.Vm.ExportierenCommand.ExecuteAsync(null);

        await a.Exporter.DidNotReceiveWithAnyArgs().ExportierenAsync(default!, default!, default!, default, default);
        Assert.Equal("", a.Vm.Meldung);
    }

    /// <summary>Spezifikation „Exportordner“: Nicht beschreibbarer Ordner → festgelegte Meldung, nichts wird geschrieben.</summary>
    [Fact]
    public async Task Export_in_nicht_beschreibbaren_Ordner()
    {
        var a = Erzeuge(beschreibbar: false);
        await MitDiagnose(a);

        await a.Vm.ExportierenCommand.ExecuteAsync(null);

        Assert.Equal("In diesen Ordner kann nicht geschrieben werden.", a.Vm.Meldung);
        Assert.True(a.Vm.MeldungIstFehler);
        await a.Exporter.DidNotReceiveWithAnyArgs().ExportierenAsync(default!, default!, default!, default, default);
    }

    /// <summary>NFA07: Ein Schreibfehler beim Export wird gemeldet, die Anwendung läuft weiter.</summary>
    [Fact]
    public async Task Export_Schreibfehler_wird_gemeldet()
    {
        var a = Erzeuge();
        a.Exporter.ExportierenAsync(default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException(new IOException("Datenträger voll.")));
        await MitDiagnose(a);

        await a.Vm.ExportierenCommand.ExecuteAsync(null);

        Assert.Contains("Datenträger voll.", a.Vm.Meldung);
        Assert.True(a.Vm.MeldungIstFehler);
        Assert.False(a.Vm.IstExportLaeuft);
    }

    /// <summary>Die Option „ohne Maskierung“ wird beim Export an den Service weitergegeben (kein Zeilenfilter).</summary>
    [Fact]
    public async Task Export_ohne_Maskierung_ohne_Filter()
    {
        var a = Erzeuge();
        await MitDiagnose(a);
        a.Vm.Maskieren = false;

        await a.Vm.ExportierenCommand.ExecuteAsync(null);

        await a.Exporter.Received(1).ExportierenAsync(
            Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), "C:\\Ziel\\bericht.zip", null, Arg.Any<CancellationToken>());
        Assert.Contains("ohne Maskierung", a.Vm.Meldung);
    }

    // ---------------------------------------------------------------- Im Browser öffnen

    private static (BerichtViewModel Vm, IBrowserOeffner Browser) MitBrowser()
    {
        var browser = Substitute.For<IBrowserOeffner>();
        browser.ZeigeHtml(Arg.Any<string>()).Returns("C:\\Temp\\Morphix\\b.html");
        var bestaetigung = Substitute.For<IBestaetigungsDialog>();
        bestaetigung.Bestaetigen(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        return (TestAufbau.Bericht(bestaetigung: bestaetigung, browser: browser), browser);
    }

    /// <summary>Ohne Diagnose lässt sich nichts im Browser öffnen (GR09).</summary>
    [Fact]
    public void Browser_ohne_Diagnose_gesperrt() =>
        Assert.False(MitBrowser().Vm.ImBrowserOeffnenCommand.CanExecute(null));

    /// <summary>Standardmäßig wird der maskierte Bericht an den Browser gegeben.</summary>
    [Fact]
    public async Task Browser_zeigt_maskierten_Bericht()
    {
        var (vm, browser) = MitBrowser();
        vm.Zeige(Diagnose());

        await vm.ImBrowserOeffnenCommand.ExecuteAsync(null);

        browser.Received(1).ZeigeHtml(Arg.Is<string>(h => h.StartsWith("<!DOCTYPE html>") && !h.Contains("10.1.2.3")));
        Assert.Contains("maskiert", vm.Meldung);
        Assert.False(vm.MeldungIstFehler);
    }

    /// <summary>Ist die Maskierung bewusst abgeschaltet, steht im Browserbericht Klartext.</summary>
    [Fact]
    public async Task Browser_zeigt_Bericht_ohne_Maskierung_wie_gewaehlt()
    {
        var (vm, browser) = MitBrowser();
        vm.Zeige(Diagnose());
        vm.Maskieren = false;

        await vm.ImBrowserOeffnenCommand.ExecuteAsync(null);

        browser.Received(1).ZeigeHtml(Arg.Is<string>(h => h.Contains("10.1.2.3")));
        Assert.Contains("ohne Maskierung", vm.Meldung);
    }

    /// <summary>Lässt sich die Datei nicht schreiben oder öffnen, gibt es eine Fehlermeldung statt eines Absturzes.</summary>
    [Fact]
    public async Task Browser_Fehler_wird_gemeldet()
    {
        var (vm, browser) = MitBrowser();
        browser.ZeigeHtml(Arg.Any<string>()).Returns(_ => throw new IOException("Kein Platz."));
        vm.Zeige(Diagnose());

        await vm.ImBrowserOeffnenCommand.ExecuteAsync(null);

        Assert.Contains("Kein Platz.", vm.Meldung);
        Assert.True(vm.MeldungIstFehler);
    }
}

/// <summary>Tests des <see cref="BrowserOeffner"/> mit einem Fake-Starter; es wird nie ein Browser gestartet.</summary>
public sealed class BrowserOeffnerTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "morphix-browser-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_ordner))
        {
            Directory.Delete(_ordner, recursive: true);
        }
    }

    /// <summary>Der Bericht wird als UTF-8-Datei geschrieben und dem Starter übergeben.</summary>
    [Fact]
    public void Schreibt_Datei_und_startet()
    {
        string? gestartet = null;
        var oeffner = new BrowserOeffner(_ordner, p => gestartet = p);

        var pfad = oeffner.ZeigeHtml("<html>Größe</html>");

        Assert.Equal(pfad, gestartet);
        Assert.EndsWith(".html", pfad);
        Assert.Equal("<html>Größe</html>", File.ReadAllText(pfad));
        Assert.StartsWith(_ordner, pfad);
    }

    /// <summary>Alte temporäre Berichte werden beim nächsten Öffnen gelöscht, neue und fremde Dateien bleiben.</summary>
    [Fact]
    public void Raeumt_alte_Berichte_auf()
    {
        Directory.CreateDirectory(_ordner);
        var alt = Path.Combine(_ordner, "Morphix_Bericht_alt.html");
        var neu = Path.Combine(_ordner, "Morphix_Bericht_neu.html");
        var fremd = Path.Combine(_ordner, "andere.html");
        foreach (var p in new[] { alt, neu, fremd })
        {
            File.WriteAllText(p, "x");
        }

        File.SetLastWriteTimeUtc(alt, DateTime.UtcNow - BrowserOeffner.Aufbewahrung - TimeSpan.FromMinutes(5));
        File.SetLastWriteTimeUtc(fremd, DateTime.UtcNow - TimeSpan.FromDays(3));

        new BrowserOeffner(_ordner, _ => { }).ZeigeHtml("x");

        Assert.False(File.Exists(alt));
        Assert.True(File.Exists(neu));
        Assert.True(File.Exists(fremd));
    }

    /// <summary>Zwei Aufrufe ergeben zwei verschiedene Dateien.</summary>
    [Fact]
    public void Jeder_Aufruf_eigene_Datei()
    {
        var oeffner = new BrowserOeffner(_ordner, _ => { });

        Assert.NotEqual(oeffner.ZeigeHtml("a"), oeffner.ZeigeHtml("b"));
    }
}
