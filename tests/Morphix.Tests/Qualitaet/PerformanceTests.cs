using System.Diagnostics;
using Morphix.Core.Domaene;
using Morphix.Infrastructure.Dateien;
using Xunit.Abstractions;

namespace Morphix.Tests.Qualitaet;

/// <summary>
/// Ein Test, der nur läuft, wenn die Umgebungsvariable <c>MORPHIX_PERFORMANCE=1</c> gesetzt ist. Normale
/// Testläufe (<c>dotnet test</c>) überspringen ihn, weil er große Dateien erzeugt und sekundenlang läuft.
/// </summary>
public sealed class PerformanceFactAttribute : FactAttribute
{
    /// <summary>Name der Umgebungsvariable, die die Performance-Tests freischaltet.</summary>
    public const string Schalter = "MORPHIX_PERFORMANCE";

    /// <summary>Erzeugt das Attribut.</summary>
    public PerformanceFactAttribute()
    {
        if (Environment.GetEnvironmentVariable(Schalter) != "1")
        {
            Skip = $"Performance-Test: nur mit {Schalter}=1 (dotnet test --filter Kategorie=Performance).";
        }
    }
}

/// <summary>
/// Performance-Tests zu NFA01 (100.000 Logzeilen in unter 5 s geladen, Filter in unter 1 s) und zur Speichergrenze
/// bei einer Million Zeilen (Technische Schuld Nr. 7). Start:
/// <c>$env:MORPHIX_PERFORMANCE=1; dotnet test -c Release --filter Kategorie=Performance --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Kategorie", "Performance")]
public sealed class PerformanceTests : IDisposable
{
    private static readonly DateTime Von = new(2026, 11, 1, 0, 0, 0);
    private static readonly DateTime Bis = new(2026, 11, 7, 23, 59, 59);

    private readonly ITestOutputHelper _ausgabe;
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "morphix-perf-" + Guid.NewGuid().ToString("N"));

    /// <summary>Legt den temporären Ordner an.</summary>
    public PerformanceTests(ITestOutputHelper ausgabe)
    {
        _ausgabe = ausgabe;
        Directory.CreateDirectory(_ordner);
    }

    /// <summary>Räumt den temporären Ordner auf.</summary>
    public void Dispose() => Directory.Delete(_ordner, recursive: true);

    private sealed record Messung(
        int Zeilen, long Bytes, int Eintraege, int Gruppen, TimeSpan Einlesen, TimeSpan Gruppieren, TimeSpan Filter, TimeSpan Suche,
        long BehaltenerSpeicher, long SpitzeArbeitsspeicher);

    private async Task<Messung> MesseAsync(int zeilen)
    {
        var pfad = Path.Combine(_ordner, "gross.log");
        var geschrieben = LogGenerator.Schreibe(pfad, zeilen, Von.AddHours(1), TimeSpan.FromDays(6));
        File.SetLastWriteTime(pfad, Von.AddDays(2));
        var quelle = new Logquelle { Pfad = _ordner, Dateimuster = "*.log" };

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var vorher = GC.GetTotalMemory(forceFullCollection: true);

        var uhr = Stopwatch.StartNew();
        var ergebnis = await new DateiLogLeser().LesenAsync(quelle, Von, Bis);
        var einlesen = uhr.Elapsed;

        uhr.Restart();
        var gruppen = FehlerGruppierer.Gruppiere(ergebnis.Eintraege);
        var gruppieren = uhr.Elapsed;

        uhr.Restart();
        var fehler = LogFilter.Anwenden(ergebnis.Eintraege, Schweregrad.Fehler, "timeout").ToList();
        var filter = uhr.Elapsed;

        uhr.Restart();
        var treffer = LogFilter.Anwenden(ergebnis.Eintraege, suchbegriff: "verbindung").ToList();
        var suche = uhr.Elapsed;

        var behalten = GC.GetTotalMemory(forceFullCollection: true) - vorher;
        GC.KeepAlive(ergebnis);
        GC.KeepAlive(gruppen);
        GC.KeepAlive(fehler);
        GC.KeepAlive(treffer);

        var messung = new Messung(
            geschrieben, new FileInfo(pfad).Length, ergebnis.Eintraege.Count, gruppen.Count, einlesen, gruppieren, filter, suche,
            behalten, Process.GetCurrentProcess().PeakWorkingSet64);
        Berichte(messung);
        return messung;
    }

    private void Berichte(Messung m)
    {
        _ausgabe.WriteLine($"--- {m.Zeilen:N0} Zeilen ({m.Bytes / 1024.0 / 1024.0:0.0} MB) ---");
        _ausgabe.WriteLine($"Einlesen + Parsen:      {m.Einlesen.TotalSeconds:0.00} s  ({m.Eintraege:N0} Einträge)");
        _ausgabe.WriteLine($"Fehler gruppieren:      {m.Gruppieren.TotalSeconds:0.00} s  ({m.Gruppen} Gruppen)");
        _ausgabe.WriteLine($"Filter Schweregrad+Text: {m.Filter.TotalMilliseconds:0} ms");
        _ausgabe.WriteLine($"Filter Suchbegriff:     {m.Suche.TotalMilliseconds:0} ms");
        _ausgabe.WriteLine($"Gehaltener Speicher:    {m.BehaltenerSpeicher / 1024.0 / 1024.0:0} MB (verwaltet, nach GC)");
        _ausgabe.WriteLine($"Spitze Arbeitsspeicher: {m.SpitzeArbeitsspeicher / 1024.0 / 1024.0:0} MB (gesamter Testprozess)");
    }

    /// <summary>NFA01: 100.000 Logzeilen in unter 5 s geladen, Filter in unter 1 s angewendet.</summary>
    [PerformanceFact]
    public async Task NFA01_100000_Zeilen()
    {
        var m = await MesseAsync(100_000);

        Assert.True(m.Eintraege > 90_000, $"Zu wenige Einträge gelesen: {m.Eintraege}");
        Assert.True(m.Einlesen < TimeSpan.FromSeconds(5), $"Einlesen dauerte {m.Einlesen.TotalSeconds:0.00} s (Vorgabe: unter 5 s).");
        Assert.True(m.Filter < TimeSpan.FromSeconds(1), $"Filter dauerte {m.Filter.TotalMilliseconds:0} ms (Vorgabe: unter 1 s).");
        Assert.True(m.Suche < TimeSpan.FromSeconds(1), $"Suche dauerte {m.Suche.TotalMilliseconds:0} ms (Vorgabe: unter 1 s).");
    }

    /// <summary>
    /// Eine Million Zeilen: Die Spezifikation nennt dafür kein Ziel. Geprüft wird, dass es ohne Absturz in
    /// vertretbarer Zeit und mit begrenztem Speicher läuft; die Messwerte stehen im Testprotokoll.
    /// </summary>
    [PerformanceFact]
    public async Task Eine_Million_Zeilen()
    {
        var m = await MesseAsync(1_000_000);

        Assert.True(m.Eintraege > 900_000, $"Zu wenige Einträge gelesen: {m.Eintraege}");
        Assert.True(m.Einlesen < TimeSpan.FromSeconds(60), $"Einlesen dauerte {m.Einlesen.TotalSeconds:0.0} s.");
        Assert.True(m.BehaltenerSpeicher < 2L * 1024 * 1024 * 1024, $"Gehaltener Speicher: {m.BehaltenerSpeicher / 1024 / 1024} MB.");
    }
}
