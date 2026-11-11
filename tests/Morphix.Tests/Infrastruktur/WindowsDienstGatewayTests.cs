using System.ComponentModel;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure.Dienste;

namespace Morphix.Tests.Infrastruktur;

/// <summary>
/// Tests des <see cref="WindowsDienstGateway"/> ausschließlich mit einem Fake der Dienstverwaltung.
/// Der echte Service Control Manager wird in automatischen Tests nie angesprochen.
/// </summary>
public class WindowsDienstGatewayTests
{
    private static readonly TimeSpan Kurz = TimeSpan.FromMilliseconds(50);

    /// <summary>Fake eines Windows-Dienstes mit steuerbarem Verhalten.</summary>
    private sealed class FakeDienst
    {
        public DienstStatus Status { get; set; } = DienstStatus.Gestoppt;
        public Starttyp Starttyp { get; set; } = Starttyp.Automatisch;
        public string Anzeigename { get; set; } = "Fake-Anzeige";
        public bool WarteLaeuftAb { get; set; }
        public Exception? FehlerBeiStart { get; set; }
        public List<string> Aufrufe { get; } = [];
        public bool Freigegeben { get; set; }
    }

    private sealed class FakeZugriff(Dictionary<string, FakeDienst> dienste) : IDienstZugriff
    {
        public Exception? FehlerBeimOeffnen { get; set; }

        public IDienstHandle? Oeffnen(string dienstname)
        {
            if (FehlerBeimOeffnen is not null)
            {
                throw FehlerBeimOeffnen;
            }

            return dienste.TryGetValue(dienstname, out var d) ? new FakeHandle(d) : null;
        }
    }

    private sealed class FakeHandle(FakeDienst d) : IDienstHandle
    {
        public string Anzeigename => d.Anzeigename;
        public DienstStatus Status => d.Status;
        public Starttyp Starttyp => d.Starttyp;
        public void Aktualisieren() { }

        public void Starten()
        {
            d.Aufrufe.Add("Starten");
            if (d.FehlerBeiStart is not null)
            {
                throw d.FehlerBeiStart;
            }

            d.Status = DienstStatus.Startet;
        }

        public void Stoppen()
        {
            d.Aufrufe.Add("Stoppen");
            d.Status = DienstStatus.Stoppt;
        }

        public bool WarteAuf(DienstStatus ziel, TimeSpan zeit)
        {
            d.Aufrufe.Add($"Warte:{ziel}");
            if (d.WarteLaeuftAb)
            {
                return false;
            }

            d.Status = ziel;
            return true;
        }

        public void Dispose() => d.Freigegeben = true;
    }

    private static (WindowsDienstGateway Gateway, FakeDienst Dienst, FakeZugriff Zugriff) Erzeuge(DienstStatus start = DienstStatus.Gestoppt)
    {
        var dienst = new FakeDienst { Status = start };
        var zugriff = new FakeZugriff(new() { ["Fake"] = dienst });
        return (new WindowsDienstGateway(zugriff, Kurz), dienst, zugriff);
    }

    private static readonly Dienst Fake = new() { Dienstname = "Fake", IstKritisch = true };

    /// <summary>Die Abfrage liefert Status, Starttyp und Anzeigename; Profilangaben bleiben erhalten.</summary>
    [Fact]
    public async Task Abfrage_liefert_Zustand()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestartet);
        dienst.Starttyp = Starttyp.Manuell;

        var ergebnis = Assert.Single(await gateway.AbfragenAsync([Fake]));

        Assert.Equal(DienstStatus.Gestartet, ergebnis.Status);
        Assert.Equal(Starttyp.Manuell, ergebnis.Starttyp);
        Assert.Equal("Fake-Anzeige", ergebnis.Anzeigename);
        Assert.True(ergebnis.IstKritisch);
        Assert.True(dienst.Freigegeben);
    }

    /// <summary>Ein Dienst, den es nicht gibt, hat den Status „Nicht gefunden“ – die übrigen werden trotzdem abgefragt.</summary>
    [Fact]
    public async Task Nicht_vorhandener_Dienst()
    {
        var (gateway, _, _) = Erzeuge();

        var ergebnis = await gateway.AbfragenAsync([new Dienst { Dienstname = "Gibt-es-nicht" }, Fake]);

        Assert.Equal([DienstStatus.NichtGefunden, DienstStatus.Gestoppt], ergebnis.Select(d => d.Status));
        Assert.Equal(["Gibt-es-nicht", "Fake"], ergebnis.Select(d => d.Dienstname));
    }

    /// <summary>Ist ein Dienst nicht lesbar, wird er als „unbekannt“ gezeigt statt die Abfrage abzubrechen.</summary>
    [Fact]
    public async Task Nicht_lesbarer_Dienst_ist_unbekannt()
    {
        var (gateway, _, zugriff) = Erzeuge();
        zugriff.FehlerBeimOeffnen = new InvalidOperationException("kein Zugriff");

        var ergebnis = Assert.Single(await gateway.AbfragenAsync([Fake]));

        Assert.Equal(DienstStatus.Unbekannt, ergebnis.Status);
    }

    /// <summary>Starten sendet die Anforderung und wartet auf „Gestartet“.</summary>
    [Fact]
    public async Task Starten_wartet_auf_Zielstatus()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestoppt);

        var ergebnis = await gateway.AusfuehrenAsync(Fake, DienstAktionsart.Starten);

        Assert.Equal(["Starten", "Warte:Gestartet"], dienst.Aufrufe);
        Assert.Equal(DienstStatus.Gestartet, ergebnis.Status);
        Assert.True(dienst.Freigegeben);
    }

    /// <summary>Stoppen sendet die Anforderung und wartet auf „Gestoppt“.</summary>
    [Fact]
    public async Task Stoppen_wartet_auf_Zielstatus()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestartet);

        var ergebnis = await gateway.AusfuehrenAsync(Fake, DienstAktionsart.Stoppen);

        Assert.Equal(["Stoppen", "Warte:Gestoppt"], dienst.Aufrufe);
        Assert.Equal(DienstStatus.Gestoppt, ergebnis.Status);
    }

    /// <summary>Neustart = erst stoppen, dann starten, jeweils mit Warten.</summary>
    [Fact]
    public async Task Neustart_stoppt_und_startet()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestartet);

        var ergebnis = await gateway.AusfuehrenAsync(Fake, DienstAktionsart.Neustarten);

        Assert.Equal(["Stoppen", "Warte:Gestoppt", "Starten", "Warte:Gestartet"], dienst.Aufrufe);
        Assert.Equal(DienstStatus.Gestartet, ergebnis.Status);
    }

    /// <summary>Ein Neustart eines gestoppten Dienstes startet ihn einfach.</summary>
    [Fact]
    public async Task Neustart_eines_gestoppten_Dienstes()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestoppt);

        await gateway.AusfuehrenAsync(Fake, DienstAktionsart.Neustarten);

        Assert.Equal(["Starten", "Warte:Gestartet"], dienst.Aufrufe);
    }

    /// <summary>Läuft der Dienst schon, gibt es nichts zu tun.</summary>
    [Fact]
    public async Task Bereits_im_Zielzustand()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestartet);

        var ergebnis = await gateway.AusfuehrenAsync(Fake, DienstAktionsart.Starten);

        Assert.Empty(dienst.Aufrufe);
        Assert.Equal(DienstStatus.Gestartet, ergebnis.Status);
    }

    /// <summary>Wird nur noch gestartet bzw. gestoppt, wird nicht erneut angefordert, sondern gewartet.</summary>
    [Fact]
    public async Task Laufende_Uebergaenge_werden_abgewartet()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Startet);

        await gateway.AusfuehrenAsync(Fake, DienstAktionsart.Starten);

        Assert.Equal(["Warte:Gestartet"], dienst.Aufrufe);
    }

    /// <summary>Wird der Zielstatus nicht rechtzeitig erreicht, kommt eine verständliche Meldung (30-Sekunden-Regel).</summary>
    [Fact]
    public async Task Timeout_wird_gemeldet()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestoppt);
        dienst.WarteLaeuftAb = true;

        var ex = await Assert.ThrowsAsync<DienstAktionFehlgeschlagenException>(() => gateway.AusfuehrenAsync(Fake, DienstAktionsart.Starten));

        Assert.Contains("nicht innerhalb von 0 Sekunden gestartet", ex.Message); // Test-Timeout 50 ms
        Assert.Contains("Fake", ex.Message);
    }

    /// <summary>Der Standard-Timeout beträgt laut Spezifikation 30 Sekunden.</summary>
    [Fact]
    public void Standard_Timeout_ist_30_Sekunden()
    {
        Assert.Equal(TimeSpan.FromSeconds(30), WindowsDienstGateway.StandardTimeout);
    }

    /// <summary>Eine Aktion auf einen nicht vorhandenen Dienst scheitert mit der Meldung der Spezifikation.</summary>
    [Fact]
    public async Task Aktion_auf_nicht_vorhandenen_Dienst()
    {
        var (gateway, _, _) = Erzeuge();

        var ex = await Assert.ThrowsAsync<DienstAktionFehlgeschlagenException>(
            () => gateway.AusfuehrenAsync(new Dienst { Dienstname = "Gibt-es-nicht" }, DienstAktionsart.Starten));

        Assert.Contains("nicht vorhanden", ex.Message);
    }

    /// <summary>Zugriff verweigert (Win32-Fehler 5) wird als Hinweis auf fehlende Administratorrechte erklärt.</summary>
    [Fact]
    public async Task Zugriff_verweigert_nennt_Administratorrechte()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestoppt);
        dienst.FehlerBeiStart = new InvalidOperationException("Fehler", new Win32Exception(5));

        var ex = await Assert.ThrowsAsync<DienstAktionFehlgeschlagenException>(() => gateway.AusfuehrenAsync(Fake, DienstAktionsart.Starten));

        Assert.Contains("verweigert", ex.Message);
        Assert.Contains("Administratorrechte", ex.Message);
        Assert.True(dienst.Freigegeben);
    }

    /// <summary>Andere Systemfehler werden mit ihrem Grund weitergegeben.</summary>
    [Fact]
    public async Task Sonstiger_Fehler_nennt_den_Grund()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestoppt);
        dienst.FehlerBeiStart = new InvalidOperationException("Der Dienst kann nicht gestartet werden.", new Win32Exception("Datei nicht gefunden"));

        var ex = await Assert.ThrowsAsync<DienstAktionFehlgeschlagenException>(() => gateway.AusfuehrenAsync(Fake, DienstAktionsart.Starten));

        Assert.Contains("Datei nicht gefunden", ex.Message);
    }

    /// <summary>Ein abgebrochener Aufruf startet nichts.</summary>
    [Fact]
    public async Task Abbruch_fuehrt_nichts_aus()
    {
        var (gateway, dienst, _) = Erzeuge(DienstStatus.Gestoppt);
        using var abbruch = new CancellationTokenSource();
        abbruch.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => gateway.AusfuehrenAsync(Fake, DienstAktionsart.Starten, abbruch.Token));

        Assert.Empty(dienst.Aufrufe);
    }

    /// <summary>Der Rechteprüfer liefert das Ergebnis der übergebenen Prüfung (ohne echte Windows-Abfrage).</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rechtepruefer_mit_Fake(bool erwartet)
    {
        Assert.Equal(erwartet, new WindowsRechtePruefer(() => erwartet).HatAdminrechte());
    }
}
