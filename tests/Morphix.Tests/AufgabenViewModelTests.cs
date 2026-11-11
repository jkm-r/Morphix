using Morphix.App.ViewModels;
using Morphix.Core.Domaene;
using Morphix.Infrastructure;
using Morphix.Infrastructure.Aufgaben;
using Morphix.Infrastructure.Ereignisse;

namespace Morphix.Tests;

/// <summary>Tests des <see cref="AufgabenViewModel"/> (US10, US11) und der Verdrahtung der Windows-Adapter.</summary>
public class AufgabenViewModelTests
{
    private static readonly DateTime T0 = new(2026, 11, 10, 2, 0, 0);

    private static Diagnose Diagnose(params GeplanteAufgabe[] aufgaben) => new()
    {
        Profil = new Systemprofil { Name = "P" },
        Von = T0,
        Bis = T0.AddDays(1),
        Aufgaben = [.. aufgaben],
        IstAbgeschlossen = true,
    };

    /// <summary>Vor der Analyse steht ein Hinweis, keine Zeilen.</summary>
    [Fact]
    public void Ohne_Diagnose()
    {
        var vm = new AufgabenViewModel();

        Assert.Empty(vm.Aufgaben);
        Assert.Equal("Noch keine Analyse durchgeführt.", vm.Hinweis);
        Assert.Equal("", vm.FehlerHinweis);
    }

    /// <summary>US10: Die Zeilen zeigen letzten Lauf, Ergebnis und nächsten Lauf.</summary>
    [Fact]
    public void Zeigt_Lauf_Ergebnis_und_naechsten_Lauf()
    {
        var vm = new AufgabenViewModel();

        vm.Zeige(Diagnose(new GeplanteAufgabe { Name = "Sicherung", LetzterLauf = T0, LetztesErgebnis = 0, NaechsterLauf = T0.AddDays(1) }));

        var zeile = Assert.Single(vm.Aufgaben);
        Assert.Equal("Sicherung", zeile.Name);
        Assert.Equal("10.11.2026 02:00", zeile.LetzterLaufText);
        Assert.Equal("11.11.2026 02:00", zeile.NaechsterLaufText);
        Assert.Equal("Erfolgreich", zeile.ErgebnisText);
        Assert.False(zeile.IstFehlgeschlagen);
        Assert.Equal("", vm.Hinweis);
        Assert.Equal("", vm.FehlerHinweis);
    }

    /// <summary>US11/GR08: Ergebniscode ungleich 0 ist fehlgeschlagen (rot) und löst den Hinweis aus.</summary>
    [Fact]
    public void Fehlgeschlagene_Aufgaben_sind_markiert()
    {
        var vm = new AufgabenViewModel();

        vm.Zeige(Diagnose(
            new GeplanteAufgabe { Name = "A", LetzterLauf = T0, LetztesErgebnis = 1 },
            new GeplanteAufgabe { Name = "B", LetzterLauf = T0, LetztesErgebnis = 0 }));

        Assert.True(vm.Aufgaben[0].IstFehlgeschlagen);
        Assert.Equal("Fehlgeschlagen (Code 0x1)", vm.Aufgaben[0].ErgebnisText);
        Assert.False(vm.Aufgaben[1].IstFehlgeschlagen);
        Assert.Equal("Achtung: 1 Aufgabe ist fehlgeschlagen.", vm.FehlerHinweis);

        vm.Zeige(Diagnose(
            new GeplanteAufgabe { Name = "A", LetztesErgebnis = 2 }, new GeplanteAufgabe { Name = "B", LetztesErgebnis = 3 }));
        Assert.Equal("Achtung: 2 Aufgaben sind fehlgeschlagen.", vm.FehlerHinweis);
    }

    /// <summary>Fehlende Angaben erscheinen als Strich bzw. „Keine Angaben“, nicht als Fehler.</summary>
    [Fact]
    public void Fehlende_Angaben()
    {
        var vm = new AufgabenViewModel();

        vm.Zeige(Diagnose(new GeplanteAufgabe { Name = "Unbekannt" }));

        var zeile = Assert.Single(vm.Aufgaben);
        Assert.Equal("–", zeile.LetzterLaufText);
        Assert.Equal("–", zeile.NaechsterLaufText);
        Assert.Equal("Keine Angaben", zeile.ErgebnisText);
        Assert.False(zeile.IstFehlgeschlagen);
    }

    /// <summary>Ein Profil ohne Aufgaben bekommt einen verständlichen Hinweis.</summary>
    [Fact]
    public void Profil_ohne_Aufgaben()
    {
        var vm = new AufgabenViewModel();

        vm.Zeige(Diagnose());

        Assert.Empty(vm.Aufgaben);
        Assert.Equal("Das Profil enthält keine geplanten Aufgaben.", vm.Hinweis);
    }

    /// <summary>Zurücksetzen (Profilwechsel) leert die Ansicht.</summary>
    [Fact]
    public void Zeige_null_setzt_zurueck()
    {
        var vm = new AufgabenViewModel();
        vm.Zeige(Diagnose(new GeplanteAufgabe { Name = "A", LetztesErgebnis = 1 }));

        vm.Zeige(null);

        Assert.Empty(vm.Aufgaben);
        Assert.Equal("", vm.FehlerHinweis);
        Assert.Equal("Noch keine Analyse durchgeführt.", vm.Hinweis);
    }

    /// <summary>Die Fabrik liefert für echte Profile die Windows-Adapter, für Demo-Profile die Demo-Adapter.</summary>
    [Fact]
    public void Fabrik_waehlt_Adapter_je_Profil()
    {
        // Es werden nur Objekte erzeugt; die Adapter lesen erst bei einer Abfrage, und die wird hier nicht gestellt.
        var fabrik = new WindowsDatenquellenFabrik(Path.Combine(AppContext.BaseDirectory, "demo"));

        var echt = fabrik.Erzeugen(new Systemprofil { Name = "Echt" });
        var demo = fabrik.Erzeugen(new Systemprofil { Name = "Demo", IstDemo = true });

        Assert.IsType<WindowsAufgabenGateway>(echt.Aufgaben);
        Assert.IsType<WindowsEreignisLeser>(echt.Ereignisse);
        Assert.IsType<Morphix.Infrastructure.Demo.DemoAufgabenGateway>(demo.Aufgaben);
        Assert.IsType<Morphix.Infrastructure.Demo.DemoEreignisLeser>(demo.Ereignisse);
    }
}
