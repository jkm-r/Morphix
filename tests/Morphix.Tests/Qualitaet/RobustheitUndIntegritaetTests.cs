using System.Security.Cryptography;
using System.Text;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure.Berichte;
using Morphix.Infrastructure.Dateien;
using NSubstitute;

namespace Morphix.Tests.Qualitaet;

/// <summary>
/// Gesamttests zu NFA07 (fehlerhafte Eingabedaten führen nicht zum Absturz) und NFA08 (Originaldaten bleiben
/// unverändert) über die ganze Kette Einlesen → Diagnose → Bericht → ZIP-Export. Dienste und Aufgaben sind Fakes;
/// es wird kein echtes System angefasst.
/// </summary>
public sealed class RobustheitUndIntegritaetTests : IDisposable
{
    private static readonly DateTime Jetzt = new(2026, 11, 8, 12, 0, 0);
    private static readonly DateTime Von = new(2026, 11, 1, 0, 0, 0);
    private static readonly DateTime Geaendert = new(2026, 11, 4, 9, 15, 0);

    private readonly string _wurzel = Path.Combine(Path.GetTempPath(), "morphix-qual-" + Guid.NewGuid().ToString("N"));
    private readonly string _logs;
    private readonly string _ziel;

    /// <summary>Legt die temporären Ordner an.</summary>
    public RobustheitUndIntegritaetTests()
    {
        _logs = Path.Combine(_wurzel, "logs");
        _ziel = Path.Combine(_wurzel, "ziel");
        Directory.CreateDirectory(_logs);
        Directory.CreateDirectory(_ziel);
    }

    /// <summary>Räumt auf.</summary>
    public void Dispose() => Directory.Delete(_wurzel, recursive: true);

    private string Schreibe(string name, byte[] inhalt)
    {
        var pfad = Path.Combine(_logs, name);
        File.WriteAllBytes(pfad, inhalt);
        File.SetLastWriteTime(pfad, Geaendert);
        return pfad;
    }

    private string Schreibe(string name, string text, Encoding? kodierung = null) =>
        Schreibe(name, (kodierung ?? new UTF8Encoding(false)).GetPreamble().Concat((kodierung ?? new UTF8Encoding(false)).GetBytes(text)).ToArray());

    private (DiagnoseService Diagnose, BerichtService Bericht) Dienste()
    {
        var dienste = Substitute.For<IDienstGateway>();
        dienste.AbfragenAsync(Arg.Any<IEnumerable<Dienst>>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult<IReadOnlyList<Dienst>>([.. c.Arg<IEnumerable<Dienst>>()]));
        var aufgaben = Substitute.For<IAufgabenGateway>();
        aufgaben.AbfragenAsync(Arg.Any<IEnumerable<GeplanteAufgabe>>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult<IReadOnlyList<GeplanteAufgabe>>([.. c.Arg<IEnumerable<GeplanteAufgabe>>()]));
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        fabrik.Erzeugen(Arg.Any<Systemprofil>()).Returns(new Datenquellen(
            new DateiLogLeser(), dienste, aufgaben, Substitute.For<IEreignisLeser>(), Substitute.For<IRechtePruefer>()));
        var zeit = new FesteUhr(Jetzt);
        return (new DiagnoseService(fabrik, zeit), new BerichtService(new HtmlZipExporter(), zeit, new Systeminfo("TESTRECHNER", "Test"), new DatenMaskierer("tester")));
    }

    private static Systemprofil Profil(string logordner) => new()
    {
        Name = "Qualitaet",
        Logquellen = [new Logquelle { Pfad = logordner, Dateimuster = "*.log" }],
        Dienste = [new Dienst { Dienstname = "Muster-Dienst" }],
        Aufgaben = [new GeplanteAufgabe { Name = "Sicherung" }],
    };

    /// <summary>
    /// NFA07: Ein Ordner mit zehn fehlerhaften Dateien (leer, binär, NUL-Bytes, falsche Kodierung, UTF-16, ohne Zeitstempel,
    /// nur Leerzeilen, eine Zeile von 2 MB, unbekannte Stufen, gesperrt) und einer guten Datei: Die Analyse läuft durch, nur die
    /// gesperrte Datei wird gemeldet, die gute Datei wird gelesen, und auch der Bericht lässt sich erzeugen.
    /// </summary>
    [Fact]
    public async Task NFA07_Ordner_mit_zehn_fehlerhaften_Dateien()
    {
        Schreibe("01-leer.log", []);
        Schreibe("02-binaer.log", RandomNumberGenerator.GetBytes(4096));
        Schreibe("03-nul.log", "2026-11-03 10:00:00 INFO vor\0dem NUL\n2026-11-03 10:00:01 ERROR Fehler mit\0NUL-Byte\n");
        Schreibe("04-ansi.log", "2026-11-03 10:00:02 ERROR Größe überschritten für Käufer\n", Encoding.GetEncoding(1252));
        Schreibe("05-utf16.log", "2026-11-03 10:00:03 WARN Unicode-Datei mit BOM\n", Encoding.Unicode);
        Schreibe("06-ohne-zeitstempel.log", "Das hier\nist kein\nLogformat\n");
        Schreibe("07-leerzeilen.log", "\n\n   \n\t\n\n");
        Schreibe("08-lange-zeile.log", "2026-11-03 10:00:04 INFO " + new string('x', 2 * 1024 * 1024));
        Schreibe("09-unbekannte-stufe.log", "2026-11-03 10:00:05 ???? komische Stufe\n2026-11-03 10:00:06 BLA weitere\n");
        var gesperrt = Schreibe("10-gesperrt.log", "2026-11-03 10:00:07 ERROR gesperrt\n");
        Schreibe("11-gut.log", "2026-11-03 11:00:00 ERROR Timeout nach 30 s bei Gegenstelle 12\n");
        var (diagnose, bericht) = Dienste();

        Diagnose ergebnis;
        await using (new FileStream(gesperrt, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            ergebnis = await diagnose.ErstellenAsync(Profil(_logs), Von, Jetzt);
        }

        Assert.True(ergebnis.IstAbgeschlossen);
        var uebersprungen = Assert.Single(ergebnis.UebersprungeneDateien);
        Assert.EndsWith("10-gesperrt.log", uebersprungen.Pfad);
        Assert.Equal(10, ergebnis.GeleseneDateien.Count);
        Assert.Contains(ergebnis.Logeintraege, e => e.Meldung.StartsWith("Timeout nach 30 s"));
        Assert.Contains(ergebnis.Logeintraege, e => e.Meldung.Contains("Käufer")); // Windows-1252-Fallback
        Assert.Contains(ergebnis.Logeintraege, e => e.Meldung.Contains("Unicode-Datei")); // UTF-16 mit BOM

        var html = bericht.Erzeugen(ergebnis);
        Assert.Contains("10-gesperrt.log", html);
        Assert.Contains("Fehlerübersicht", html);
    }

    /// <summary>Ein Ordner, der nur aus fehlerhaften Dateien besteht, ergibt eine leere, aber gültige Diagnose.</summary>
    [Fact]
    public async Task NFA07_nur_fehlerhafte_Dateien_ergeben_leere_Diagnose()
    {
        Schreibe("a.log", []);
        Schreibe("b.log", RandomNumberGenerator.GetBytes(2048));
        Schreibe("c.log", "kein Zeitstempel\n");
        var (diagnose, bericht) = Dienste();

        var ergebnis = await diagnose.ErstellenAsync(Profil(_logs), Von, Jetzt);

        Assert.True(ergebnis.IstAbgeschlossen);
        Assert.Empty(ergebnis.Fehlergruppen);
        Assert.Contains("keine Fehler gefunden", bericht.Erzeugen(ergebnis));
    }

    private static List<(string Name, string Hash, DateTime Zeit, long Groesse)> Fingerabdruck(string ordner) =>
        [.. Directory.GetFiles(ordner)
            .OrderBy(p => p, StringComparer.Ordinal)
            .Select(p => (Path.GetFileName(p), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))), File.GetLastWriteTimeUtc(p), new FileInfo(p).Length))];

    /// <summary>
    /// NFA08/GR05: Nach Analyse, Berichtserzeugung und ZIP-Export (mit und ohne Maskierung) haben alle Logdateien denselben
    /// SHA-256-Wert, dasselbe Änderungsdatum und dieselbe Größe, und im Logordner ist nichts hinzugekommen.
    /// </summary>
    [Fact]
    public async Task NFA08_Originale_bleiben_bei_Analyse_und_Export_unveraendert()
    {
        Schreibe("a.log", "2026-11-03 10:00:00 ERROR Anmeldung user=anna von 192.168.1.5\n   bei X()\n2026-11-03 10:00:05 INFO ok\n");
        Schreibe("b.log", "2026-11-04 08:00:00 WARN Größe überschritten\n", Encoding.GetEncoding(1252));
        Schreibe("c.log", RandomNumberGenerator.GetBytes(1024));
        var vorher = Fingerabdruck(_logs);
        var (diagnose, bericht) = Dienste();

        var ergebnis = await diagnose.ErstellenAsync(Profil(_logs), Von, Jetzt);
        await bericht.ExportierenAsync(ergebnis, maskieren: true, Path.Combine(_ziel, "maskiert.zip"));
        await bericht.ExportierenAsync(ergebnis, maskieren: false, Path.Combine(_ziel, "klartext.zip"));

        Assert.Equal(vorher, Fingerabdruck(_logs));
        Assert.True(File.Exists(Path.Combine(_ziel, "maskiert.zip")));
        Assert.True(File.Exists(Path.Combine(_ziel, "klartext.zip")));
    }
}
