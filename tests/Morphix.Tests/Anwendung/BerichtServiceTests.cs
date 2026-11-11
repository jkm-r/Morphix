using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests.Anwendung;

/// <summary>Tests des <see cref="BerichtService"/>: GR09, GR10, Inhalt, HTML-Kodierung und Export.</summary>
public class BerichtServiceTests
{
    private static readonly DateTime Jetzt = new(2026, 11, 11, 14, 30, 0);
    private static readonly DateTime T0 = new(2026, 11, 10, 8, 0, 0);

    private static BerichtService Service(IBerichtExporter? exporter = null) => new(
        exporter ?? Substitute.For<IBerichtExporter>(),
        new FesteUhr(Jetzt),
        new Systeminfo("SRV-MUSTER", "Test-Windows 11"),
        new DatenMaskierer("tester"));

    private static Diagnose Diagnose(
        bool abgeschlossen = true,
        IEnumerable<Logeintrag>? eintraege = null,
        IEnumerable<Dienst>? dienste = null,
        IEnumerable<GeplanteAufgabe>? aufgaben = null,
        IEnumerable<UebersprungeneDatei>? uebersprungen = null,
        IEnumerable<string>? dateien = null,
        string profilname = "Demo – Musterwerk")
    {
        var liste = (eintraege ?? []).OrderBy(e => e.Zeitstempel).ToList();
        return new Diagnose
        {
            Profil = new Systemprofil { Name = profilname, Beschreibung = "Beschreibung" },
            Von = T0,
            Bis = T0.AddDays(1),
            ErstelltAm = Jetzt,
            Logeintraege = liste,
            Fehlergruppen = FehlerGruppierer.Gruppiere(liste),
            Dienste = [.. dienste ?? []],
            Aufgaben = [.. aufgaben ?? []],
            UebersprungeneDateien = [.. uebersprungen ?? []],
            GeleseneDateien = [.. dateien ?? []],
            IstAbgeschlossen = abgeschlossen,
        };
    }

    private static Logeintrag Fehler(string meldung, int minuten = 0, string quelle = "a.log") =>
        new(T0.AddMinutes(minuten), Schweregrad.Fehler, quelle, meldung);

    // ------------------------------------------------------------------ GR09

    /// <summary>GR09: Aus einer nicht abgeschlossenen Diagnose entsteht kein Bericht.</summary>
    [Fact]
    public async Task GR09_Bericht_nur_aus_abgeschlossener_Diagnose()
    {
        var service = Service();
        var offen = Diagnose(abgeschlossen: false);

        var ex = Assert.Throws<InvalidOperationException>(() => service.Erzeugen(offen));
        Assert.Equal(BerichtService.KeineDiagnoseMeldung, ex.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportierenAsync(offen, true, "x.zip"));
    }

    /// <summary>Ohne Diagnose ist das ein Programmierfehler, kein GR09-Fall.</summary>
    [Fact]
    public void Ohne_Diagnose_ArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => Service().Erzeugen(null!));

    // ------------------------------------------------------------------ Inhalt

    /// <summary>US13: Systeminfo, Dienststatus, Fehlerübersicht und übersprungene Dateien stehen im Bericht.</summary>
    [Fact]
    public void Bericht_enthaelt_alle_Abschnitte()
    {
        var html = Service().Erzeugen(Diagnose(
            eintraege: [Fehler("Timeout nach 30 s bei Gegenstelle 12", 1), Fehler("Timeout nach 45 s bei Gegenstelle 7", 2)],
            dienste:
            [
                new Dienst { Dienstname = "Muster-Dienst", Anzeigename = "Musterdienst", Status = DienstStatus.Gestoppt, Starttyp = Starttyp.Automatisch, IstKritisch = true },
            ],
            uebersprungen: [new UebersprungeneDatei("C:\\Logs\\kaputt.log", "Datei ist gesperrt")]),
            maskieren: true);

        Assert.Contains("Systeminformationen", html);
        Assert.Contains("Test-Windows 11", html);
        Assert.Contains("11.11.2026 14:30:00", html);
        Assert.Contains("10.11.2026 08:00:00 bis 11.11.2026 08:00:00", html);
        Assert.Contains("Demo – Musterwerk", html);
        Assert.Contains("Dienststatus", html);
        Assert.Contains("Musterdienst", html);
        Assert.Contains("Gestoppt", html);
        Assert.Contains("Fehlerübersicht", html);
        Assert.Contains("Timeout nach # s bei Gegenstelle #", html);
        Assert.Contains(">2<", html);
        Assert.Contains("kaputt.log", html);
        Assert.Contains("Datei ist gesperrt", html);
    }

    /// <summary>Der Bericht ist eine selbstständige Datei: eingebettetes CSS, kein Skript, keine externen Verweise.</summary>
    [Fact]
    public void Bericht_ist_selbststaendig()
    {
        var html = Service().Erzeugen(Diagnose(eintraege: [Fehler("Fehler 1")]));

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<style>", html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>US13: Aufgaben erscheinen nur, wenn das Profil welche hat; fehlgeschlagene sind markiert (GR08).</summary>
    [Fact]
    public void Aufgaben_nur_wenn_vorhanden()
    {
        var ohne = Service().Erzeugen(Diagnose());
        Assert.DoesNotContain("Geplante Aufgaben", ohne);
        Assert.DoesNotContain("Aufgaben fehlgeschlagen", ohne);

        var mit = Service().Erzeugen(Diagnose(aufgaben:
        [
            new GeplanteAufgabe { Name = "Sicherung", LetzterLauf = T0, LetztesErgebnis = 1, NaechsterLauf = T0.AddDays(1) },
            new GeplanteAufgabe { Name = "Bereinigung", LetztesErgebnis = 0 },
        ]));
        Assert.Contains("Geplante Aufgaben", mit);
        Assert.Contains("fehlgeschlagen (Code 0x1)", mit);
        Assert.Contains("erfolgreich (0)", mit);
        Assert.Contains("class=\"rot\"", mit);
    }

    /// <summary>US13: Ereignisse erscheinen nur, wenn die Analyse welche geliefert hat.</summary>
    [Fact]
    public void Ereignisse_nur_wenn_vorhanden()
    {
        var ohne = Service().Erzeugen(Diagnose(eintraege: [Fehler("a")]));
        Assert.DoesNotContain("Ereignisanzeige", ohne);

        var mit = Service().Erzeugen(Diagnose(eintraege:
        [
            Fehler("a"),
            new Logeintrag(T0.AddHours(2), Schweregrad.Warnung, Logeintrag.EreignisQuelle, "Zeitdienst reagiert nicht"),
        ]));
        Assert.Contains("<h2>Ereignisanzeige</h2>", mit);
        Assert.Contains("Zeitdienst reagiert nicht", mit);
    }

    /// <summary>Ohne Fehler, Dienste und übersprungene Dateien zeigt der Bericht verständliche Leertexte.</summary>
    [Fact]
    public void Leere_Diagnose_ergibt_Leertexte()
    {
        var html = Service().Erzeugen(Diagnose());

        Assert.Contains("Im Zeitraum wurden keine Fehler gefunden.", html);
        Assert.Contains("Keine Dienste im Profil.", html);
        Assert.Contains("Es wurde keine Datei übersprungen.", html);
    }

    /// <summary>Die Fehlerübersicht ist begrenzt; der Bericht sagt, dass weitere Gruppen fehlen.</summary>
    [Fact]
    public void Fehlergruppen_sind_begrenzt()
    {
        var viele = Enumerable.Range(0, 105)
            .Select(i => Fehler($"Eigener Fehler Nr {(char)('A' + i % 26)}{(char)('a' + i / 26)}", i, quelle: $"q{i}.log"));

        var html = Service().Erzeugen(Diagnose(eintraege: viele));

        Assert.Contains("Weitere 5 Fehlergruppen sind nicht aufgeführt", html);
    }

    /// <summary>Sehr lange Meldungen (Stacktraces) werden gekürzt.</summary>
    [Fact]
    public void Lange_Meldungen_werden_gekuerzt()
    {
        var html = Service().Erzeugen(Diagnose(eintraege: [Fehler("Kopf" + Environment.NewLine + new string('x', 20000))]));

        // HtmlEncode kodiert Umlaute numerisch (ü = &#252;); der Browser zeigt sie korrekt an.
        Assert.Contains("(gek&#252;rzt)", html);
        Assert.True(html.Length < 15000);
    }

    // ------------------------------------------------------------------ HTML-Kodierung

    /// <summary>Alle Inhalte aus Logs, Profil und Diensten werden kodiert; eingebettete Skripte werden nicht ausgeführt.</summary>
    [Fact]
    public void Inhalte_werden_HTML_kodiert()
    {
        const string boese = "<script>alert('x')</script>";
        var html = Service().Erzeugen(Diagnose(
            eintraege: [new Logeintrag(T0, Schweregrad.Fehler, "<b>q</b>.log", boese)],
            dienste: [new Dienst { Dienstname = boese, Anzeigename = "<img src=x onerror=alert(1)>" }],
            aufgaben: [new GeplanteAufgabe { Name = boese }],
            uebersprungen: [new UebersprungeneDatei("<i>pfad</i>", boese)],
            profilname: "<u>Profil</u>"),
            maskieren: false);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<b>q", html);
        Assert.DoesNotContain("<i>pfad", html);
        Assert.DoesNotContain("<u>Profil", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    // ------------------------------------------------------------------ GR10 Maskierung

    private static Diagnose DiagnoseMitSensiblemInhalt() => Diagnose(
        eintraege:
        [
            Fehler("Anmeldung fehlgeschlagen user=anna von 192.168.10.25"),
            new Logeintrag(T0.AddMinutes(1), Schweregrad.Fehler, "a.log", "Zugriff verweigert für MUSTER\\berta, tester meldet Fehler"),
        ]);

    /// <summary>GR10: Mit Maskierung stehen weder IP-Adressen noch Benutzernamen noch der Rechnername im Bericht.</summary>
    [Fact]
    public void Maskierung_macht_Daten_unkenntlich()
    {
        var html = Service().Erzeugen(DiagnoseMitSensiblemInhalt(), maskieren: true);

        Assert.DoesNotContain("192.168.10.25", html);
        Assert.DoesNotContain("anna", html);
        Assert.DoesNotContain("berta", html);
        Assert.DoesNotContain("tester", html);
        Assert.DoesNotContain("SRV-MUSTER", html);
        Assert.Contains(DatenMaskierer.IpErsatz, html);
        Assert.Contains(DatenMaskierer.BenutzerErsatz, html);
        Assert.Contains("[Rechner]", html);
        Assert.Contains("aktiv", html);
    }

    /// <summary>Wird die Maskierung bewusst abgeschaltet, steht alles im Klartext, und der Bericht sagt es.</summary>
    [Fact]
    public void Ohne_Maskierung_steht_Klartext_im_Bericht()
    {
        var html = Service().Erzeugen(DiagnoseMitSensiblemInhalt(), maskieren: false);

        Assert.Contains("192.168.10.25", html);
        Assert.Contains("user=anna", html);
        Assert.Contains("SRV-MUSTER", html);
        Assert.Contains("ausgeschaltet", html);
    }

    /// <summary>GR10: Ohne Angabe ist die Maskierung an.</summary>
    [Fact]
    public void Maskierung_ist_standardmaessig_an() =>
        Assert.DoesNotContain("192.168.10.25", Service().Erzeugen(DiagnoseMitSensiblemInhalt()));

    // ------------------------------------------------------------------ Export

    /// <summary>Der Export gibt dem Exporter den Bericht, die Logdateien und – bei Maskierung – den Zeilenfilter.</summary>
    [Fact]
    public async Task Export_mit_Maskierung_uebergibt_Zeilenfilter()
    {
        var exporter = Substitute.For<IBerichtExporter>();
        Func<string, string>? filter = null;
        await exporter.ExportierenAsync(
            Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<string>(),
            Arg.Do<Func<string, string>?>(f => filter = f), Arg.Any<CancellationToken>());
        var diagnose = Diagnose(dateien: ["C:\\Logs\\a.log"]);

        var bericht = await Service(exporter).ExportierenAsync(diagnose, true, "C:\\Ziel\\bericht.zip");

        Assert.NotNull(filter);
        Assert.Equal($"von {DatenMaskierer.IpErsatz}", filter("von 10.1.2.3"));
        Assert.True(bericht.Maskiert);
        Assert.Equal("C:\\Ziel\\bericht.zip", bericht.Dateipfad);
        Assert.Equal(Jetzt, bericht.ErstelltAm);
        await exporter.Received(1).ExportierenAsync(
            Arg.Is<string>(h => h.StartsWith("<!DOCTYPE html>")),
            Arg.Is<IEnumerable<string>>(d => d.SequenceEqual(new[] { "C:\\Logs\\a.log" })),
            "C:\\Ziel\\bericht.zip",
            Arg.Any<Func<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>Ohne Maskierung kopiert der Exporter die Logs unverändert (kein Filter).</summary>
    [Fact]
    public async Task Export_ohne_Maskierung_uebergibt_keinen_Filter()
    {
        var exporter = Substitute.For<IBerichtExporter>();

        var bericht = await Service(exporter).ExportierenAsync(Diagnose(), false, "x.zip");

        Assert.False(bericht.Maskiert);
        await exporter.Received(1).ExportierenAsync(
            Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), "x.zip", null, Arg.Any<CancellationToken>());
    }

    // ------------------------------------------------------------------ Dateiname und Zielordner

    /// <summary>Dateiname: Morphix_&lt;Profil&gt;_&lt;yyyyMMdd_HHmm&gt;.zip, ungültige Zeichen werden ersetzt.</summary>
    [Theory]
    [InlineData("Musterwerk", "Morphix_Musterwerk_20261111_1430.zip")]
    [InlineData("Demo – Musterwerk Server", "Morphix_Demo_–_Musterwerk_Server_20261111_1430.zip")]
    [InlineData("a/b:c*d", "Morphix_a_b_c_d_20261111_1430.zip")]
    [InlineData("   ", "Morphix_Profil_20261111_1430.zip")]
    public void Dateinamenvorschlag(string profil, string erwartet) =>
        Assert.Equal(erwartet, Service().Dateinamenvorschlag(Diagnose(profilname: profil)));

    /// <summary>Spezifikation „Exportordner“: Ist der Ordner nicht beschreibbar, kommt die festgelegte Meldung.</summary>
    [Fact]
    public void Zielordner_nicht_beschreibbar_liefert_Meldung()
    {
        var exporter = Substitute.For<IBerichtExporter>();
        exporter.KannSchreiben("gut.zip").Returns(true);
        exporter.KannSchreiben("schlecht.zip").Returns(false);
        var service = Service(exporter);

        Assert.Null(service.PruefeZielpfad("gut.zip"));
        Assert.Equal("In diesen Ordner kann nicht geschrieben werden.", service.PruefeZielpfad("schlecht.zip"));
        Assert.Equal(BerichtService.ExportordnerMeldung, service.PruefeZielpfad(""));
    }
}
