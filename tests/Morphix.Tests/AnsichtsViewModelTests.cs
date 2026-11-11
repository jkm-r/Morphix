using Morphix.App.ViewModels;
using Morphix.Core.Domaene;

namespace Morphix.Tests;

/// <summary>Tests der ViewModels von Dashboard sowie Logs und Fehler.</summary>
public class AnsichtsViewModelTests
{
    private static readonly DateTime T0 = new(2026, 11, 1, 8, 0, 0);

    private static Logeintrag E(string meldung, int minuten, Schweregrad grad = Schweregrad.Fehler, string quelle = "a.log") =>
        new(T0.AddMinutes(minuten), grad, quelle, meldung);

    private static Diagnose DiagnoseMit(IEnumerable<Logeintrag> eintraege, IEnumerable<Dienst>? dienste = null,
        IEnumerable<GeplanteAufgabe>? aufgaben = null, IEnumerable<UebersprungeneDatei>? uebersprungen = null)
    {
        var liste = eintraege.OrderBy(e => e.Zeitstempel).ToList();
        return new Diagnose
        {
            Profil = new Systemprofil { Name = "P" },
            Von = T0,
            Bis = T0.AddDays(1),
            Logeintraege = liste,
            Fehlergruppen = FehlerGruppierer.Gruppiere(liste),
            Dienste = [.. dienste ?? []],
            Aufgaben = [.. aufgaben ?? []],
            UebersprungeneDateien = [.. uebersprungen ?? []],
            IstAbgeschlossen = true,
        };
    }

    // ---------------------------------------------------------------- Dashboard

    /// <summary>Vor der Analyse zeigen die Kennzahlen einen Platzhalter, „Bericht erstellen“ ist gesperrt.</summary>
    [Fact]
    public void Dashboard_ohne_Diagnose()
    {
        var vm = new DashboardViewModel();

        Assert.Equal("–", vm.FehlerAnzahl);
        Assert.False(vm.BerichtErstellenCommand.CanExecute(null));
        Assert.Empty(vm.HaeufigsteGruppen);
    }

    /// <summary>Die drei Kennzahlen entsprechen dem Mock-up: Fehler, gestoppte kritische Dienste, fehlgeschlagene Aufgaben.</summary>
    [Fact]
    public void Dashboard_Kennzahlen()
    {
        var vm = new DashboardViewModel();
        var diagnose = DiagnoseMit(
            [E("Timeout 1", 1), E("Timeout 2", 2), E("Kritisch", 3, Schweregrad.Kritisch), E("Info", 4, Schweregrad.Info), E("Warn", 5, Schweregrad.Warnung)],
            dienste:
            [
                new Dienst { Dienstname = "A", IstKritisch = true, Status = DienstStatus.Gestoppt },
                new Dienst { Dienstname = "B", IstKritisch = false, Status = DienstStatus.Gestoppt },
                new Dienst { Dienstname = "C", IstKritisch = true, Status = DienstStatus.Gestartet },
            ],
            aufgaben:
            [
                new GeplanteAufgabe { Name = "x", LetztesErgebnis = 1 },
                new GeplanteAufgabe { Name = "y", LetztesErgebnis = 0 },
                new GeplanteAufgabe { Name = "z", LetztesErgebnis = 5 },
            ]);

        vm.Zeige(diagnose);

        Assert.Equal("3", vm.FehlerAnzahl);
        Assert.Equal("1", vm.GestoppteKritischeDienste);
        Assert.Equal("Kritischer Dienst gestoppt", vm.KritischeDiensteText);
        Assert.Equal("2", vm.FehlgeschlageneAufgaben);
        Assert.Equal("Aufgaben fehlgeschlagen", vm.AufgabenText);
        Assert.True(vm.BerichtErstellenCommand.CanExecute(null));
    }

    /// <summary>Es werden höchstens die fünf häufigsten Gruppen gezeigt, die häufigste zuerst.</summary>
    [Fact]
    public void Dashboard_zeigt_Top_Gruppen()
    {
        var eintraege = Enumerable.Range(1, 7)
            .SelectMany(g => Enumerable.Range(0, g).Select(i => E($"Fehler Art{(char)('A' + g)}", g * 10 + i)))
            .ToList();
        var vm = new DashboardViewModel();

        vm.Zeige(DiagnoseMit(eintraege));

        Assert.Equal(5, vm.HaeufigsteGruppen.Count);
        Assert.Equal([7, 6, 5, 4, 3], vm.HaeufigsteGruppen.Select(g => g.Anzahl));
    }

    /// <summary>Ohne Fehler und mit übersprungenen Dateien erscheint ein Hinweis (NFA07).</summary>
    [Fact]
    public void Dashboard_Hinweise()
    {
        var vm = new DashboardViewModel();

        vm.Zeige(DiagnoseMit([E("Info", 1, Schweregrad.Info)], uebersprungen: [new UebersprungeneDatei(@"C:\Logs\x.log", "gesperrt")]));

        Assert.Contains("keine Fehler", vm.Hinweis);
        Assert.Contains("x.log", vm.Hinweis);
        Assert.Contains("1 Datei(en) übersprungen", vm.Hinweis);
    }

    /// <summary>„Bericht erstellen“ löst die Navigation zur Berichtsansicht aus.</summary>
    [Fact]
    public void Dashboard_Bericht_erstellen_meldet_sich()
    {
        var vm = new DashboardViewModel();
        vm.Zeige(DiagnoseMit([]));
        var ausgeloest = false;
        vm.BerichtAngefordert += () => ausgeloest = true;

        vm.BerichtErstellenCommand.Execute(null);

        Assert.True(ausgeloest);
    }

    /// <summary>Wird die Diagnose verworfen (z. B. Profilwechsel), kehrt das Dashboard in den Ausgangszustand zurück.</summary>
    [Fact]
    public void Dashboard_Zuruecksetzen()
    {
        var vm = new DashboardViewModel();
        vm.Zeige(DiagnoseMit([E("x", 1)]));

        vm.Zeige(null);

        Assert.Equal("–", vm.FehlerAnzahl);
        Assert.Empty(vm.HaeufigsteGruppen);
        Assert.False(vm.BerichtErstellenCommand.CanExecute(null));
    }

    // ---------------------------------------------------------------- Logs und Fehler

    private static LogsUndFehlerViewModel LogsMit(params Logeintrag[] eintraege)
    {
        var vm = new LogsUndFehlerViewModel();
        vm.Zeige(DiagnoseMit(eintraege));
        return vm;
    }

    private static readonly Logeintrag[] Beispiel =
    [
        E("Timeout nach 30 s bei Terminal 1", 1),
        E("Timeout nach 45 s bei Terminal 2", 2),
        E("Datenbank verloren", 3, quelle: "db.log"),
        E("Lizenz läuft ab", 4, Schweregrad.Warnung),
        E("Dienst gestartet", 5, Schweregrad.Info),
    ];

    /// <summary>Ohne Filter erscheinen alle Einträge und alle Fehlergruppen.</summary>
    [Fact]
    public void Logs_ohne_Filter()
    {
        var vm = LogsMit(Beispiel);

        Assert.Equal(5, vm.Eintraege.Count);
        Assert.Equal(2, vm.Gruppen.Count);
        Assert.Contains("5 Einträge, 2 Fehlergruppen", vm.Zusammenfassung);
    }

    /// <summary>Die Filter wirken sofort und sind kombinierbar (US04).</summary>
    [Fact]
    public void Logs_Filter_wirken_sofort_und_kombiniert()
    {
        var vm = LogsMit(Beispiel);

        vm.AusgewaehlterSchweregrad = vm.Schweregrade.First(s => s.Wert == Schweregrad.Fehler);
        Assert.Equal(3, vm.Eintraege.Count);

        vm.Suchbegriff = "timeout";
        Assert.Equal(2, vm.Eintraege.Count);
        Assert.Single(vm.Gruppen);

        vm.BisTag = T0.Date.AddDays(-1);
        Assert.Empty(vm.Eintraege);
        Assert.Empty(vm.Gruppen);

        vm.BisTag = null;
        vm.Suchbegriff = "";
        vm.AusgewaehlterSchweregrad = vm.Schweregrade[0];
        Assert.Equal(5, vm.Eintraege.Count);
    }

    /// <summary>Der Info-Filter zeigt Einträge, erzeugt aber keine Fehlergruppen (GR07).</summary>
    [Fact]
    public void Logs_Info_Filter_hat_keine_Gruppen()
    {
        var vm = LogsMit(Beispiel);

        vm.AusgewaehlterSchweregrad = vm.Schweregrade.First(s => s.Wert == Schweregrad.Info);

        Assert.Single(vm.Eintraege);
        Assert.Empty(vm.Gruppen);
    }

    /// <summary>Ein zu langer Suchbegriff zeigt die Meldung der Spezifikation und filtert nicht.</summary>
    [Fact]
    public void Logs_zu_langer_Suchbegriff()
    {
        var vm = LogsMit(Beispiel);

        vm.Suchbegriff = new string('x', 201);

        Assert.Equal("Der Suchbegriff ist zu lang.", vm.SuchbegriffFehler);
        Assert.Equal(5, vm.Eintraege.Count);

        vm.Suchbegriff = "Lizenz";
        Assert.Equal("", vm.SuchbegriffFehler);
        Assert.Single(vm.Eintraege);
    }

    /// <summary>Doppelklick auf eine Gruppe zeigt deren Einträge (US05); „Alle Einträge“ hebt das auf.</summary>
    [Fact]
    public void Logs_Doppelklick_zeigt_Eintraege_der_Gruppe()
    {
        var vm = LogsMit(Beispiel);
        var timeouts = vm.Gruppen.First(g => g.Meldungsmuster.StartsWith("Timeout"));

        vm.GruppeOeffnenCommand.Execute(timeouts);

        Assert.Equal(timeouts.Meldungsmuster, vm.GeoeffneteGruppe?.Meldungsmuster);
        Assert.Equal(2, vm.Eintraege.Count);
        Assert.All(vm.Eintraege, e => Assert.StartsWith("Timeout", e.Meldung));
        Assert.Contains("Timeout nach # s bei Terminal #", vm.EintraegeTitel);

        vm.AlleEintraegeAnzeigenCommand.Execute(null);

        Assert.Null(vm.GeoeffneteGruppe);
        Assert.Equal(5, vm.Eintraege.Count);
    }

    /// <summary>Verschwindet die geöffnete Gruppe durch einen Filter, werden wieder alle gefilterten Einträge gezeigt.</summary>
    [Fact]
    public void Logs_geoeffnete_Gruppe_verschwindet_durch_Filter()
    {
        var vm = LogsMit(Beispiel);
        vm.GruppeOeffnenCommand.Execute(vm.Gruppen.First(g => g.Meldungsmuster.StartsWith("Timeout")));

        vm.Suchbegriff = "Datenbank";

        Assert.Null(vm.GeoeffneteGruppe);
        Assert.Single(vm.Eintraege);
    }

    /// <summary>Ein <c>null</c>-Parameter (nichts gewählt) bewirkt nichts.</summary>
    [Fact]
    public void Logs_Doppelklick_ohne_Auswahl()
    {
        var vm = LogsMit(Beispiel);

        vm.GruppeOeffnenCommand.Execute(null);

        Assert.Null(vm.GeoeffneteGruppe);
        Assert.Equal(5, vm.Eintraege.Count);
    }

    /// <summary>Sehr viele Einträge werden begrenzt angezeigt; die Zusammenfassung nennt die Gesamtzahl.</summary>
    [Fact]
    public void Logs_begrenzt_die_Anzeige()
    {
        var viele = Enumerable.Range(0, LogsUndFehlerViewModel.MaxAngezeigteEintraege + 50)
            .Select(i => E("Info", i, Schweregrad.Info))
            .ToArray();

        var vm = LogsMit(viele);

        Assert.Equal(LogsUndFehlerViewModel.MaxAngezeigteEintraege, vm.Eintraege.Count);
        Assert.Contains("10.050", vm.Zusammenfassung);
    }

    /// <summary>Ohne Diagnose sind die Listen leer; die Filter bleiben beim Zurücksetzen erhalten.</summary>
    [Fact]
    public void Logs_ohne_Diagnose_und_Zuruecksetzen()
    {
        var vm = new LogsUndFehlerViewModel();
        Assert.Empty(vm.Eintraege);

        vm.Zeige(DiagnoseMit(Beispiel));
        vm.Suchbegriff = "Lizenz";
        vm.Zeige(null);

        Assert.Empty(vm.Eintraege);
        Assert.Equal("Lizenz", vm.Suchbegriff);
    }

    /// <summary>Performance (NFA01-nah): Filtern von 100.000 Einträgen dauert deutlich unter einer Sekunde.</summary>
    [Fact]
    public void Logs_Filter_bei_100000_Eintraegen_ist_schnell()
    {
        var viele = Enumerable.Range(0, 100_000)
            .Select(i => E($"Meldung {i}", i % 5000, i % 50 == 0 ? Schweregrad.Fehler : Schweregrad.Info))
            .ToArray();
        var vm = LogsMit(viele);

        var uhr = System.Diagnostics.Stopwatch.StartNew();
        vm.Suchbegriff = "Meldung 9";
        uhr.Stop();

        Assert.True(uhr.ElapsedMilliseconds < 1000, $"Filter dauerte {uhr.ElapsedMilliseconds} ms");
    }
}
