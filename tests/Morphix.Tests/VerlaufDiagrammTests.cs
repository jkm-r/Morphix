using Morphix.App.ViewModels;
using Morphix.Core.Domaene;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace Morphix.Tests;

/// <summary>Tests des Diagramms im Dashboard (US12).</summary>
public class VerlaufDiagrammTests
{
    private static readonly DateTime T0 = new(2026, 11, 10, 0, 0, 0);

    private static Diagnose DiagnoseMit(DateTime von, DateTime bis, params Logeintrag[] eintraege) => new()
    {
        Profil = new Systemprofil { Name = "P" },
        Von = von,
        Bis = bis,
        Logeintraege = [.. eintraege],
        IstAbgeschlossen = true,
    };

    private static RectangleBarSeries Reihe(OxyPlot.PlotModel modell, string titel) =>
        modell.Series.OfType<RectangleBarSeries>().Single(s => s.Title == titel);

    /// <summary>Vor der Analyse gibt es kein Diagramm.</summary>
    [Fact]
    public void Ohne_Diagnose_kein_Diagramm()
    {
        var vm = new DashboardViewModel();

        Assert.Null(vm.Diagramm);
        Assert.False(vm.HatDiagramm);
    }

    /// <summary>Nach der Analyse zeigt das Diagramm gestapelte Säulen: Kritisch unten, Fehler darüber, je Tag.</summary>
    [Fact]
    public void Diagramm_enthaelt_Fehler_und_Kritisch_je_Tag()
    {
        var vm = new DashboardViewModel();

        vm.Zeige(DiagnoseMit(T0, T0.AddDays(3).AddSeconds(-1),
            new Logeintrag(T0.AddHours(3), Schweregrad.Fehler, "a", "x"),
            new Logeintrag(T0.AddHours(4), Schweregrad.Fehler, "a", "y"),
            new Logeintrag(T0.AddDays(2), Schweregrad.Kritisch, "a", "z"),
            new Logeintrag(T0.AddDays(2).AddHours(1), Schweregrad.Fehler, "a", "w")));

        Assert.True(vm.HatDiagramm);
        Assert.Equal("Fehler pro Tag", vm.Diagramm!.Title);
        var fehler = Reihe(vm.Diagramm, "Fehler");
        var kritisch = Reihe(vm.Diagramm, "Kritisch");

        // Tag 1: zwei Fehler; Tag 3: ein kritischer Eintrag unten (0 bis 1), darüber ein Fehler (1 bis 2). Leere Säulen fehlen.
        Assert.Equal(2, fehler.Items.Count);
        Assert.Equal((0.0, 2.0), (fehler.Items[0].Y0, fehler.Items[0].Y1));
        Assert.Equal((1.0, 2.0), (fehler.Items[1].Y0, fehler.Items[1].Y1));
        var einzelnes = Assert.Single(kritisch.Items);
        Assert.Equal((0.0, 1.0), (einzelnes.Y0, einzelnes.Y1));
        Assert.Equal("Fehler: 2", fehler.Items[0].Title);
        Assert.Equal("Kritisch: 1", einzelnes.Title);

        // Die Säulen liegen innerhalb ihres Tages und überlappen die Nachbartage nicht.
        var tag3Beginn = DateTimeAxis.ToDouble(T0.AddDays(2));
        var tag3Ende = DateTimeAxis.ToDouble(T0.AddDays(3));
        Assert.InRange(einzelnes.X0, tag3Beginn, tag3Ende);
        Assert.InRange(einzelnes.X1, tag3Beginn, tag3Ende);
        Assert.True(einzelnes.X0 < einzelnes.X1);
    }

    /// <summary>Die Datumsachse umfasst alle Zeitabschnitte; Tageswerte werden als „dd.MM.“ beschriftet.</summary>
    [Fact]
    public void Datumsachse_umfasst_den_Zeitraum()
    {
        var vm = new DashboardViewModel();

        vm.Zeige(DiagnoseMit(T0, T0.AddDays(3).AddSeconds(-1)));

        var achse = vm.Diagramm!.Axes.OfType<DateTimeAxis>().Single();
        Assert.Equal(DateTimeAxis.ToDouble(T0), achse.Minimum);
        Assert.Equal(DateTimeAxis.ToDouble(T0.AddDays(3)), achse.Maximum);
        Assert.Equal("dd.MM.", achse.StringFormat);
    }

    /// <summary>Bei einem Zeitraum bis 48 Stunden zeigt das Diagramm Stunden.</summary>
    [Fact]
    public void Kurzer_Zeitraum_zeigt_Stunden()
    {
        var vm = new DashboardViewModel();

        vm.Zeige(DiagnoseMit(T0.AddHours(8), T0.AddHours(10).AddMinutes(30),
            new Logeintrag(T0.AddHours(9).AddMinutes(15), Schweregrad.Fehler, "a", "x")));

        Assert.Equal("Fehler pro Stunde", vm.Diagramm!.Title);
        var achse = vm.Diagramm.Axes.OfType<DateTimeAxis>().Single();
        Assert.Equal("dd.MM. HH:mm", achse.StringFormat);
        Assert.Equal(DateTimeAxis.ToDouble(T0.AddHours(8)), achse.Minimum);
        Assert.Equal(DateTimeAxis.ToDouble(T0.AddHours(11)), achse.Maximum);
        var item = Assert.Single(Reihe(vm.Diagramm, "Fehler").Items);
        Assert.InRange(item.X0, DateTimeAxis.ToDouble(T0.AddHours(9)), DateTimeAxis.ToDouble(T0.AddHours(10)));
    }

    /// <summary>Ohne Fehler bleibt die Wertachse bei einem sinnvollen Bereich (Maximum größer als 0), und es gibt keine Säulen.</summary>
    [Fact]
    public void Ohne_Fehler_hat_die_Achse_einen_Bereich()
    {
        var vm = new DashboardViewModel();

        vm.Zeige(DiagnoseMit(T0, T0.AddDays(2)));

        var achse = vm.Diagramm!.Axes.Single(a => a is LinearAxis and not DateTimeAxis);
        Assert.Equal(0, achse.Minimum);
        Assert.True(achse.Maximum > 0);
        Assert.All(vm.Diagramm.Series.OfType<RectangleBarSeries>(), s => Assert.Empty(s.Items));
    }

    /// <summary>Zurücksetzen (Profilwechsel) entfernt das Diagramm.</summary>
    [Fact]
    public void Zeige_null_entfernt_das_Diagramm()
    {
        var vm = new DashboardViewModel();
        vm.Zeige(DiagnoseMit(T0, T0.AddDays(2)));

        vm.Zeige(null);

        Assert.Null(vm.Diagramm);
        Assert.False(vm.HatDiagramm);
    }
}
