using Morphix.Core.Domaene;
using Morphix.Infrastructure;
using Morphix.Infrastructure.Demo;
using Morphix.Infrastructure.Profile;

namespace Morphix.Tests.Infrastruktur;

/// <summary>Tests der Demo-Adapter und der <see cref="WindowsDatenquellenFabrik"/>.</summary>
public class DemoAdapterTests
{
    private static readonly string DemoOrdner = System.IO.Path.Combine(AppContext.BaseDirectory, "demo");
    private static readonly DateTime Jetzt = new(2026, 11, 11, 12, 0, 0);

    private static async Task<Systemprofil> DemoProfil() =>
        (await new JsonProfilRepository("x", "y", System.IO.Path.Combine(DemoOrdner, "demo-profil.json")).LadeDemoProfilAsync())!;

    private static Logquelle Quelle => new() { Pfad = "logs", Dateimuster = "*.log" };

    /// <summary>Der jüngste Eintrag liegt bei „jetzt“; der Standardzeitraum liefert Treffer.</summary>
    [Fact]
    public async Task LogLeser_verschiebt_Zeitstempel_auf_jetzt()
    {
        var leser = new DemoLogLeser(DemoOrdner, () => Jetzt);

        var ergebnis = await leser.LesenAsync(Quelle, Jetzt.AddDays(-7), Jetzt);

        Assert.Equal(3, ergebnis.Dateianzahl);
        Assert.Empty(ergebnis.Uebersprungen);
        Assert.True(ergebnis.Bytes > 0);
        Assert.Equal(Jetzt, ergebnis.Eintraege.Max(e => e.Zeitstempel));
        Assert.InRange(ergebnis.Eintraege.Count, 1000, 3000);
    }

    /// <summary>Ein Teilzeitraum liefert weniger Einträge; Einträge liegen sortiert im Zeitraum.</summary>
    [Fact]
    public async Task LogLeser_filtert_nach_Zeitraum()
    {
        var leser = new DemoLogLeser(DemoOrdner, () => Jetzt);
        var alle = await leser.LesenAsync(Quelle, Jetzt.AddDays(-30), Jetzt);

        var teil = await leser.LesenAsync(Quelle, Jetzt.AddDays(-2), Jetzt);

        Assert.True(teil.Eintraege.Count < alle.Eintraege.Count);
        Assert.All(teil.Eintraege, e => Assert.InRange(e.Zeitstempel, Jetzt.AddDays(-2), Jetzt));
        Assert.Equal(teil.Eintraege.OrderBy(e => e.Zeitstempel), teil.Eintraege);
    }

    /// <summary>Die Demo-Logs enthalten die geplanten Fehlerarten, Stacktraces und Maskierungskandidaten.</summary>
    [Fact]
    public async Task LogLeser_Demo_Daten_enthalten_die_geplanten_Inhalte()
    {
        var leser = new DemoLogLeser(DemoOrdner, () => Jetzt);

        var eintraege = (await leser.LesenAsync(Quelle, Jetzt.AddDays(-30), Jetzt)).Eintraege;
        var gruppen = FehlerGruppierer.Gruppiere(eintraege);

        Assert.Contains(gruppen, g => g.Meldungsmuster == "Timeout nach # s bei Terminal #" && g.Anzahl > 10);
        Assert.Contains(gruppen, g => g.Meldungsmuster == "Datenbankverbindung verloren");
        Assert.Contains(gruppen, g => g.Meldungsmuster == "Export-Job beendet mit Code #");
        Assert.Contains(eintraege, e => e.Schweregrad == Schweregrad.Warnung && e.Meldung.Contains("Lizenz"));
        Assert.Contains(eintraege, e => e.Meldung.Contains('\n')); // mehrzeilige Stacktraces (GR06)
        Assert.Contains(eintraege, e => e.Meldung.Contains("192.168."));
        Assert.Contains(eintraege, e => e.Meldung.Contains("MUSTERWERK\\"));
        Assert.Equal(3, eintraege.Select(e => e.Quelle).Distinct().Count());
    }

    /// <summary>Ein nicht vorhandener Ordner ergibt ein leeres Ergebnis statt eines Fehlers.</summary>
    [Fact]
    public async Task LogLeser_fehlender_Ordner()
    {
        var leser = new DemoLogLeser(DemoOrdner, () => Jetzt);

        var ergebnis = await leser.LesenAsync(new Logquelle { Pfad = "gibt-es-nicht" }, Jetzt.AddDays(-1), Jetzt);

        Assert.Empty(ergebnis.Eintraege);
        Assert.Equal(0, ergebnis.Dateianzahl);
    }

    /// <summary>Das Einlesen meldet den Fortschritt bis 100 Prozent.</summary>
    [Fact]
    public async Task LogLeser_meldet_Fortschritt()
    {
        var werte = new List<int>();
        var leser = new DemoLogLeser(DemoOrdner, () => Jetzt);

        await leser.LesenAsync(Quelle, Jetzt.AddDays(-7), Jetzt, new Progress<int>(werte.Add));
        await Task.Delay(100); // Progress<T> meldet asynchron über den SynchronizationContext

        Assert.Equal(100, werte.Max());
    }

    /// <summary>Die Demo-Dienste starten im Zustand aus dem Profil; ein Dienst ist gestoppt.</summary>
    [Fact]
    public async Task DienstGateway_liefert_Anfangszustand()
    {
        var profil = await DemoProfil();
        var gateway = new DemoDienstGateway(profil.Dienste, TimeSpan.Zero);

        var dienste = await gateway.AbfragenAsync(profil.Dienste);

        var gestoppt = Assert.Single(dienste, d => d.Status == DienstStatus.Gestoppt);
        Assert.True(gestoppt.IstKritisch);
        Assert.Equal(3, dienste.Count(d => d.Status == DienstStatus.Gestartet));
    }

    /// <summary>Aktionen verändern nur den Zustand im Speicher (GR04).</summary>
    [Fact]
    public async Task DienstGateway_simuliert_Aktionen()
    {
        var profil = await DemoProfil();
        var gateway = new DemoDienstGateway(profil.Dienste, TimeSpan.Zero);
        var dienst = profil.Dienste.First(d => d.Status == DienstStatus.Gestoppt);

        var gestartet = await gateway.AusfuehrenAsync(dienst, DienstAktionsart.Starten);
        Assert.Equal(DienstStatus.Gestartet, gestartet.Status);

        await gateway.AusfuehrenAsync(dienst, DienstAktionsart.Stoppen);
        Assert.Equal(DienstStatus.Gestoppt, (await gateway.AbfragenAsync([dienst]))[0].Status);

        var neu = await gateway.AusfuehrenAsync(dienst, DienstAktionsart.Neustarten);
        Assert.Equal(DienstStatus.Gestartet, neu.Status);
    }

    /// <summary>Unbekannte Dienste kommen als „nicht gefunden“ zurück; eine Aktion darauf scheitert.</summary>
    [Fact]
    public async Task DienstGateway_unbekannter_Dienst()
    {
        var gateway = new DemoDienstGateway([], TimeSpan.Zero);
        var fremd = new Dienst { Dienstname = "Fremd" };

        Assert.Equal(DienstStatus.NichtGefunden, (await gateway.AbfragenAsync([fremd]))[0].Status);
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.AusfuehrenAsync(fremd, DienstAktionsart.Starten));
    }

    /// <summary>Die Demo-Aufgaben: nur der Lohnexport ist fehlgeschlagen (GR08); der nächste Lauf liegt in der Zukunft.</summary>
    [Fact]
    public async Task AufgabenGateway_liefert_feste_Beispieldaten()
    {
        var profil = await DemoProfil();
        var gateway = new DemoAufgabenGateway(() => Jetzt);

        var aufgaben = await gateway.AbfragenAsync(profil.Aufgaben);

        var fehlgeschlagen = Assert.Single(aufgaben, a => a.IstFehlgeschlagen);
        Assert.Equal("Nächtlicher Lohnexport", fehlgeschlagen.Name);
        Assert.Equal(1, fehlgeschlagen.LetztesErgebnis);
        Assert.All(aufgaben, a =>
        {
            Assert.True(a.LetzterLauf <= Jetzt);
            Assert.True(a.NaechsterLauf > Jetzt);
        });
    }

    /// <summary>Unbekannte Aufgaben bleiben unverändert.</summary>
    [Fact]
    public async Task AufgabenGateway_unbekannte_Aufgabe()
    {
        var a = new GeplanteAufgabe { Name = "Unbekannt" };

        var ergebnis = await new DemoAufgabenGateway(() => Jetzt).AbfragenAsync([a]);

        Assert.Equal(a, ergebnis[0]);
    }

    /// <summary>Ereignisse werden auf den Zeitraum eingeschränkt.</summary>
    [Fact]
    public async Task EreignisLeser_filtert_nach_Zeitraum()
    {
        var leser = new DemoEreignisLeser(() => Jetzt);
        var quelle = new Logquelle { Typ = LogquellenTyp.Ereignisanzeige, Pfad = "Application" };

        var alle = await leser.LesenAsync(quelle, Jetzt.AddDays(-30), Jetzt);
        var teil = await leser.LesenAsync(quelle, Jetzt.AddDays(-1), Jetzt);

        Assert.Equal(4, alle.Count);
        Assert.Single(teil);
        Assert.All(alle, e => Assert.NotEqual(Schweregrad.Info, e.Schweregrad));
    }

    /// <summary>Die Fabrik liefert für Demo-Profile die Demo-Adapter (kein <c>if (istDemo)</c> in den Services).</summary>
    [Fact]
    public void Fabrik_liefert_Demo_Adapter_fuer_Demo_Profile()
    {
        var fabrik = new WindowsDatenquellenFabrik(DemoOrdner);

        var quellen = fabrik.Erzeugen(new Systemprofil { Name = "Demo", IstDemo = true });

        Assert.IsType<DemoLogLeser>(quellen.LogLeser);
        Assert.IsType<DemoDienstGateway>(quellen.Dienste);
        Assert.IsType<DemoAufgabenGateway>(quellen.Aufgaben);
        Assert.IsType<DemoEreignisLeser>(quellen.Ereignisse);
    }
}
