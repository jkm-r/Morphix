using System.Diagnostics;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure.Aufgaben;
using Morphix.Infrastructure.Dienste;
using Morphix.Infrastructure.Profile;
using NSubstitute;

namespace Morphix.Tests.Qualitaet;

/// <summary>
/// NFA11 (Anforderung v1.1), Teil 3: Profile mit bis zu 50 Diensten und 50 Aufgaben; die Statusabfrage aller Dienste dauert
/// unter einer Sekunde. Die Tests verwenden ausschließlich Fakes (keine echten Dienste und Aufgaben) und messen damit den
/// Aufwand von Morphix selbst, nicht die Antwortzeit des Service Control Managers (siehe Testprotokoll).
/// </summary>
public sealed class GrenzwertTests : IDisposable
{
    private const int Anzahl = 50;

    private readonly string _wurzel = Path.Combine(Path.GetTempPath(), "morphix-grenze-" + Guid.NewGuid().ToString("N"));

    /// <summary>Legt den temporären Ordner an.</summary>
    public GrenzwertTests()
    {
        Directory.CreateDirectory(Path.Combine(_wurzel, "logs"));
    }

    /// <summary>Räumt auf.</summary>
    public void Dispose() => Directory.Delete(_wurzel, recursive: true);

    private Systemprofil GrossesProfil() => new()
    {
        Name = "Grosses Profil",
        Logquellen = [new Logquelle { Pfad = Path.Combine(_wurzel, "logs"), Dateimuster = "*.log" }],
        Dienste = [.. Enumerable.Range(1, Anzahl).Select(i => new Dienst { Dienstname = $"Muster-Dienst-{i:D2}", IstKritisch = i % 5 == 0 })],
        Aufgaben = [.. Enumerable.Range(1, Anzahl).Select(i => new GeplanteAufgabe { Name = $"Muster-Aufgabe-{i:D2}" })],
    };

    /// <summary>Ein Fake-Dienst ohne Verzögerung.</summary>
    private sealed class SchnellerHandle(DienstStatus status) : IDienstHandle
    {
        public string Anzeigename => "Fake";
        public DienstStatus Status => status;
        public Starttyp Starttyp => Starttyp.Automatisch;
        public void Aktualisieren() { }
        public void Starten() => throw new NotSupportedException("Tests steuern keine Dienste.");
        public void Stoppen() => throw new NotSupportedException("Tests steuern keine Dienste.");
        public bool WarteAuf(DienstStatus ziel, TimeSpan zeit) => throw new NotSupportedException("Tests steuern keine Dienste.");
        public void Dispose() { }
    }

    private sealed class SchnellerZugriff : IDienstZugriff
    {
        public IDienstHandle? Oeffnen(string dienstname) => new SchnellerHandle(
            dienstname.EndsWith("07", StringComparison.Ordinal) ? DienstStatus.Gestoppt : DienstStatus.Gestartet);
    }

    /// <summary>Ein Profil mit 50 Diensten und 50 Aufgaben ist gültig (GR02, Feldregeln).</summary>
    [Fact]
    public void Profil_mit_50_Diensten_und_50_Aufgaben_ist_gueltig()
    {
        var ergebnis = new ProfilValidator().Pruefen(GrossesProfil(), []);

        Assert.True(ergebnis.IstGueltig, string.Join("; ", ergebnis.Fehler.Select(f => f.Meldung)));
    }

    /// <summary>Ein solches Profil lässt sich als JSON speichern und unverändert wieder laden (NFA10).</summary>
    [Fact]
    public async Task Profil_mit_50_Diensten_und_50_Aufgaben_laesst_sich_speichern_und_laden()
    {
        var repo = new JsonProfilRepository(Path.Combine(_wurzel, "Profile"), Path.Combine(_wurzel, "einstellungen.json"), Path.Combine(_wurzel, "demo.json"));
        var profil = GrossesProfil();

        await repo.SpeichernAsync(profil);
        var geladen = Assert.Single(await repo.LadenAlleAsync());

        Assert.Equal(Anzahl, geladen.Dienste.Count);
        Assert.Equal(Anzahl, geladen.Aufgaben.Count);
        Assert.Equal(profil.Dienste.Select(d => (d.Dienstname, d.IstKritisch)), geladen.Dienste.Select(d => (d.Dienstname, d.IstKritisch)));
        Assert.Equal(profil.Aufgaben.Select(a => a.Name), geladen.Aufgaben.Select(a => a.Name));
    }

    /// <summary>NFA11: Die Statusabfrage aller 50 Dienste dauert unter einer Sekunde; die Reihenfolge bleibt erhalten.</summary>
    [Fact]
    public async Task Statusabfrage_von_50_Diensten_unter_1_Sekunde()
    {
        var gateway = new WindowsDienstGateway(new SchnellerZugriff());
        var profil = GrossesProfil();
        await gateway.AbfragenAsync(profil.Dienste); // Aufwärmen

        var uhr = Stopwatch.StartNew();
        var ergebnis = await gateway.AbfragenAsync(profil.Dienste);
        uhr.Stop();

        Assert.Equal(Anzahl, ergebnis.Count);
        Assert.Equal(profil.Dienste.Select(d => d.Dienstname), ergebnis.Select(d => d.Dienstname));
        Assert.Equal(1, ergebnis.Count(d => d.Status == DienstStatus.Gestoppt && d.Dienstname.EndsWith("07", StringComparison.Ordinal)));
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(1), $"Die Abfrage dauerte {uhr.ElapsedMilliseconds} ms (Vorgabe: unter 1 s).");
    }

    /// <summary>Die ganze Analyse eines Profils mit 50 Diensten und 50 Aufgaben (Fakes) bleibt unter einer Sekunde.</summary>
    [Fact]
    public async Task Analyse_mit_50_Diensten_und_50_Aufgaben_unter_1_Sekunde()
    {
        File.WriteAllText(Path.Combine(_wurzel, "logs", "a.log"), "2026-11-03 10:00:00 ERROR Timeout 1\n");
        File.SetLastWriteTime(Path.Combine(_wurzel, "logs", "a.log"), new DateTime(2026, 11, 4));
        var aufgabenZugriff = Substitute.For<IAufgabenZugriff>();
        aufgabenZugriff.Lesen(Arg.Any<string>()).Returns(new AufgabenZustand(new DateTime(2026, 11, 3, 2, 0, 0), 0, null));
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        fabrik.Erzeugen(Arg.Any<Systemprofil>()).Returns(new Datenquellen(
            new Morphix.Infrastructure.Dateien.DateiLogLeser(),
            new WindowsDienstGateway(new SchnellerZugriff()),
            new WindowsAufgabenGateway(aufgabenZugriff),
            Substitute.For<IEreignisLeser>(),
            Substitute.For<IRechtePruefer>()));
        var service = new DiagnoseService(fabrik, new FesteUhr(new DateTime(2026, 11, 8, 12, 0, 0)));
        var profil = GrossesProfil();
        await service.ErstellenAsync(profil, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7)); // Aufwärmen

        var uhr = Stopwatch.StartNew();
        var diagnose = await service.ErstellenAsync(profil, new DateTime(2026, 11, 1), new DateTime(2026, 11, 7));
        uhr.Stop();

        Assert.Equal(Anzahl, diagnose.Dienste.Count);
        Assert.Equal(Anzahl, diagnose.Aufgaben.Count);
        Assert.All(diagnose.Aufgaben, a => Assert.Equal(0, a.LetztesErgebnis));
        Assert.True(uhr.Elapsed < TimeSpan.FromSeconds(1), $"Die Analyse dauerte {uhr.ElapsedMilliseconds} ms (Vorgabe für die Dienstabfrage: unter 1 s).");
    }
}
