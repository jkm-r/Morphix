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
/// Performance-Tests zu NFA01 (100.000 Logzeilen in unter 5 s geladen, Filter in unter 1 s) und NFA11 (Anforderung v1.1:
/// bis 1.000.000 Zeilen bzw. 500 Dateien, höchstens linear wachsende Einlesezeit unter 50 s, Arbeitsspeicher unter 1 GB). Start:
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
    /// NFA11 (v1.1), Teil 1: bis zu 1.000.000 Logzeilen ohne Absturz, Einlesezeit höchstens linear wachsend und unter 50 s,
    /// Arbeitsspeicher unter 1 GB. Das lineare Wachstum wird aus zwei Größen abgeleitet: Das Zehnfache an Zeilen darf höchstens
    /// das Zwanzigfache an Zeit brauchen (Faktor 2 Toleranz für Messrauschen bei der sehr kurzen 100.000-Zeilen-Messung).
    /// </summary>
    [PerformanceFact]
    public async Task NFA11_Eine_Million_Zeilen_linear_und_unter_1_GB()
    {
        await MesseAsync(20_000); // Aufwärmen (JIT), damit die kleine Messung nicht verfälscht wird
        var klein = await MesseAsync(100_000);
        var gross = await MesseAsync(1_000_000);

        Assert.True(gross.Eintraege > 900_000, $"Zu wenige Einträge gelesen: {gross.Eintraege}");
        Assert.True(gross.Einlesen < TimeSpan.FromSeconds(50), $"Einlesen dauerte {gross.Einlesen.TotalSeconds:0.0} s (Vorgabe: unter 50 s).");
        Assert.True(
            gross.Einlesen.TotalSeconds <= 20 * Math.Max(klein.Einlesen.TotalSeconds, 0.05),
            $"Einlesezeit wächst stärker als linear: 100.000 Zeilen {klein.Einlesen.TotalSeconds:0.00} s, 1.000.000 Zeilen {gross.Einlesen.TotalSeconds:0.00} s.");
        AssertUnterEinemGigabyte(gross);
    }

    /// <summary>
    /// NFA11 (v1.1), Teil 2: 500 Dateien (je 2.000 Zeilen, zusammen 1.000.000 Zeilen) in einem Ordner ohne Absturz, unter 50 s
    /// und unter 1 GB Arbeitsspeicher.
    /// </summary>
    [PerformanceFact]
    public async Task NFA11_500_Dateien()
    {
        const int Dateien = 500;
        const int ZeilenProDatei = 2_000;
        for (var i = 0; i < Dateien; i++)
        {
            var pfad = Path.Combine(_ordner, $"anwendung-{i:D3}.log");
            LogGenerator.Schreibe(pfad, ZeilenProDatei, Von.AddHours(1).AddMinutes(i), TimeSpan.FromHours(6));
            File.SetLastWriteTime(pfad, Von.AddDays(2));
        }

        GC.Collect();
        var vorher = GC.GetTotalMemory(forceFullCollection: true);
        var uhr = Stopwatch.StartNew();
        var ergebnis = await new DateiLogLeser().LesenAsync(new Logquelle { Pfad = _ordner, Dateimuster = "*.log" }, Von, Bis);
        var einlesen = uhr.Elapsed;
        var gruppen = FehlerGruppierer.Gruppiere(ergebnis.Eintraege);
        var behalten = GC.GetTotalMemory(forceFullCollection: true) - vorher;
        var spitze = Process.GetCurrentProcess().PeakWorkingSet64;
        GC.KeepAlive(ergebnis);
        GC.KeepAlive(gruppen);

        _ausgabe.WriteLine($"--- {Dateien} Dateien, {Dateien * ZeilenProDatei:N0} Zeilen ---");
        _ausgabe.WriteLine($"Einlesen + Parsen:      {einlesen.TotalSeconds:0.00} s  ({ergebnis.Eintraege.Count:N0} Einträge, {ergebnis.Dateianzahl} Dateien)");
        _ausgabe.WriteLine($"Gehaltener Speicher:    {behalten / 1024.0 / 1024.0:0} MB");
        _ausgabe.WriteLine($"Spitze Arbeitsspeicher: {spitze / 1024.0 / 1024.0:0} MB (gesamter Testprozess)");

        Assert.Equal(Dateien, ergebnis.Dateianzahl);
        Assert.Empty(ergebnis.Uebersprungen);
        Assert.True(ergebnis.Eintraege.Count > 900_000, $"Zu wenige Einträge gelesen: {ergebnis.Eintraege.Count}");
        Assert.True(einlesen < TimeSpan.FromSeconds(50), $"Einlesen dauerte {einlesen.TotalSeconds:0.0} s (Vorgabe: unter 50 s).");
        Assert.True(behalten < Gigabyte, $"Gehaltener Speicher: {behalten / 1024 / 1024} MB (Vorgabe: unter 1 GB).");
        Assert.True(spitze < Gigabyte, $"Spitze des Arbeitsspeichers: {spitze / 1024 / 1024} MB (Vorgabe: unter 1 GB).");
    }

    private const long Gigabyte = 1024L * 1024 * 1024;

    private static void AssertUnterEinemGigabyte(Messung m)
    {
        Assert.True(m.BehaltenerSpeicher < Gigabyte, $"Gehaltener Speicher: {m.BehaltenerSpeicher / 1024 / 1024} MB (Vorgabe: unter 1 GB).");
        Assert.True(m.SpitzeArbeitsspeicher < Gigabyte, $"Spitze des Arbeitsspeichers: {m.SpitzeArbeitsspeicher / 1024 / 1024} MB (Vorgabe: unter 1 GB).");
    }
}
