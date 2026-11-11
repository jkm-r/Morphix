using Morphix.Core.Domaene;
using Morphix.Infrastructure.Protokoll;

namespace Morphix.Tests.Infrastruktur;

/// <summary>Tests des <see cref="JsonLinesAktionsProtokoll"/> mit einem temporären Ordner (US16).</summary>
public sealed class JsonLinesAktionsProtokollTests : IDisposable
{
    private static readonly DateTime T0 = new(2026, 11, 11, 15, 0, 0);

    private readonly string _wurzel = Path.Combine(Path.GetTempPath(), "morphix-protokoll-" + Guid.NewGuid().ToString("N"));

    private string Pfad => Path.Combine(_wurzel, "daten", "aktionen.jsonl");

    /// <summary>Räumt den temporären Ordner auf.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_wurzel))
        {
            Directory.Delete(_wurzel, recursive: true);
        }
    }

    private static Dienstaktion Eintrag(int minute, DienstAktionsart art = DienstAktionsart.Starten, bool erfolg = true) =>
        new(T0.AddMinutes(minute), art, "tester", erfolg)
        {
            Dienstname = "Muster-Dienst",
            Profil = "Demo – Musterwerk",
            Simuliert = true,
            Meldung = $"Dienst „Muster-Dienst“ wurde gestartet ({minute}).",
        };

    /// <summary>Ohne Datei ist das Protokoll leer; der Ordner entsteht erst beim ersten Eintrag.</summary>
    [Fact]
    public async Task Ohne_Datei_leer()
    {
        var protokoll = new JsonLinesAktionsProtokoll(Pfad);

        Assert.Empty(await protokoll.LeseAsync());
        Assert.False(Directory.Exists(Path.GetDirectoryName(Pfad)));
    }

    /// <summary>Ein Eintrag wird vollständig gespeichert und gelesen, auch Umlaute und Anführungszeichen (Roundtrip).</summary>
    [Fact]
    public async Task Roundtrip_mit_allen_Feldern()
    {
        var protokoll = new JsonLinesAktionsProtokoll(Pfad);
        var eintrag = Eintrag(0, DienstAktionsart.Neustarten, erfolg: false);

        await protokoll.ProtokolliereAsync(eintrag);
        var gelesen = Assert.Single(await protokoll.LeseAsync());

        Assert.Equal(eintrag, gelesen);
    }

    /// <summary>Die Datei enthält je Eintrag genau eine Zeile, lesbare Umlaute und die Aktion als Text.</summary>
    [Fact]
    public async Task Datei_ist_JSON_Lines_und_lesbar()
    {
        var protokoll = new JsonLinesAktionsProtokoll(Pfad);

        await protokoll.ProtokolliereAsync(Eintrag(0, DienstAktionsart.Stoppen));
        await protokoll.ProtokolliereAsync(Eintrag(1) with { Meldung = "Zeile 1\nZeile 2" });

        var zeilen = File.ReadAllLines(Pfad);
        Assert.Equal(2, zeilen.Length);
        Assert.Contains("\"Aktion\":\"Stoppen\"", zeilen[0]);
        Assert.Contains("Musterwerk", zeilen[0]);
        Assert.Contains("„Muster-Dienst“", zeilen[0]);
        Assert.Contains("Zeile 1\\nZeile 2", zeilen[1]);
    }

    /// <summary>Die jüngsten Einträge kommen zuerst, die Anzahl ist begrenzt.</summary>
    [Fact]
    public async Task Neueste_zuerst_und_begrenzt()
    {
        var protokoll = new JsonLinesAktionsProtokoll(Pfad);
        for (var i = 0; i < 5; i++)
        {
            await protokoll.ProtokolliereAsync(Eintrag(i));
        }

        var alle = await protokoll.LeseAsync(100);
        var zwei = await protokoll.LeseAsync(2);

        Assert.Equal([4, 3, 2, 1, 0], alle.Select(e => (int)(e.Zeitpunkt - T0).TotalMinutes));
        Assert.Equal([4, 3], zwei.Select(e => (int)(e.Zeitpunkt - T0).TotalMinutes));
        Assert.Empty(await protokoll.LeseAsync(0));
    }

    /// <summary>Das Protokoll wird nur angehängt: Eine neue Instanz (neuer Programmstart) ergänzt, statt zu überschreiben.</summary>
    [Fact]
    public async Task Neue_Instanz_haengt_an()
    {
        await new JsonLinesAktionsProtokoll(Pfad).ProtokolliereAsync(Eintrag(0));
        await new JsonLinesAktionsProtokoll(Pfad).ProtokolliereAsync(Eintrag(1));

        Assert.Equal(2, (await new JsonLinesAktionsProtokoll(Pfad).LeseAsync()).Count);
    }

    /// <summary>Eine beschädigte Zeile verhindert nicht das Lesen der übrigen.</summary>
    [Fact]
    public async Task Beschaedigte_Zeile_wird_uebersprungen()
    {
        var protokoll = new JsonLinesAktionsProtokoll(Pfad);
        await protokoll.ProtokolliereAsync(Eintrag(0));
        await File.AppendAllTextAsync(Pfad, "{\"Zeitpunkt\": kaputt\n\nkeine json zeile\n");
        await protokoll.ProtokolliereAsync(Eintrag(1));

        var gelesen = await protokoll.LeseAsync();

        Assert.Equal(2, gelesen.Count);
    }

    /// <summary>Viele gleichzeitige Schreibvorgänge ergeben lauter ganze Zeilen und keinen Verlust.</summary>
    [Fact]
    public async Task Gleichzeitiges_Schreiben_ist_sicher()
    {
        var protokoll = new JsonLinesAktionsProtokoll(Pfad);

        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => protokoll.ProtokolliereAsync(Eintrag(i))));

        Assert.Equal(50, File.ReadAllLines(Pfad).Length);
        Assert.Equal(50, (await protokoll.LeseAsync(100)).Count);
    }

    /// <summary>Lässt sich nicht schreiben, kommt eine IOException (der Service meldet das, ohne die Aktion zu stören).</summary>
    [Fact]
    public async Task Nicht_beschreibbar_wirft_IOException()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Pfad)!);
        await using var gesperrt = new FileStream(Pfad, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

        await Assert.ThrowsAsync<IOException>(() => new JsonLinesAktionsProtokoll(Pfad).ProtokolliereAsync(Eintrag(0)));
    }
}
