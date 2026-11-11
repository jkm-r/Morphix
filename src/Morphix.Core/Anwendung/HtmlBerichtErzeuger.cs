using System.Globalization;
using System.Net;
using System.Text;
using Morphix.Core.Domaene;

namespace Morphix.Core.Anwendung;

/// <summary>
/// Baut den Support-Bericht als eine selbstständige HTML-Datei (eingebettetes CSS, kein Skript, keine
/// externen Ressourcen). Jeder Text, der aus Logs, Profil oder System stammt, geht durch <see cref="Text"/>:
/// erst maskieren (GR10), dann mit <see cref="WebUtility.HtmlEncode(string?)"/> kodieren, damit eingebettete
/// Skripte nicht ausgeführt werden (Architekturdokument, Kap. 5).
/// </summary>
internal sealed class HtmlBerichtErzeuger
{
    // Begrenzungen halten den Bericht lesbar und klein; die vollständigen Logs liegen im ZIP.
    internal const int MaxFehlergruppen = 100;
    internal const int MaxEreignisse = 100;
    internal const int MaxMeldungsZeichen = 4000;

    private static readonly CultureInfo Kultur = CultureInfo.GetCultureInfo("de-DE");
    private const string Zeitformat = "dd.MM.yyyy HH:mm:ss";

    private readonly Diagnose _diagnose;
    private readonly Systeminfo _system;
    private readonly DateTime _erstelltAm;
    private readonly bool _maskieren;
    private readonly Func<string, string> _maskiere;
    private readonly StringBuilder _html = new();

    public HtmlBerichtErzeuger(
        Diagnose diagnose, Systeminfo system, DateTime erstelltAm, bool maskieren, Func<string, string> maskiere)
    {
        _diagnose = diagnose;
        _system = system;
        _erstelltAm = erstelltAm;
        _maskieren = maskieren;
        _maskiere = maskiere;
    }

    /// <summary>Erzeugt das vollständige HTML-Dokument.</summary>
    public string Erzeugen()
    {
        _html.Clear();
        var profil = _diagnose.Profil;

        _html.AppendLine("<!DOCTYPE html>");
        _html.AppendLine("<html lang=\"de\">");
        _html.AppendLine("<head>");
        _html.AppendLine("<meta charset=\"utf-8\">");
        _html.AppendLine($"<title>Morphix Support-Bericht – {Text(profil.Name)}</title>");
        _html.AppendLine($"<style>{Stil}</style>");
        _html.AppendLine("</head>");
        _html.AppendLine("<body>");
        _html.AppendLine("<h1>Morphix Support-Bericht</h1>");
        _html.AppendLine($"<p class=\"untertitel\">{Text(profil.Name)}</p>");

        Systeminformationen();
        Zusammenfassung();
        Dienststatus();
        Fehleruebersicht();
        Aufgaben();
        Ereignisse();
        UebersprungeneDateien();

        _html.AppendLine("<p class=\"fuss\">Dieser Bericht wurde lokal mit Morphix erzeugt. Es wurden keine Daten übertragen.</p>");
        _html.AppendLine("</body>");
        _html.AppendLine("</html>");
        return _html.ToString();
    }

    private void Systeminformationen()
    {
        var profil = _diagnose.Profil;
        _html.AppendLine("<h2>Systeminformationen</h2>");
        _html.AppendLine("<table class=\"info\">");
        Zeile("Rechnername", _maskieren ? "[Rechner]" : _system.Rechnername, maskierenErlaubt: false);
        Zeile("Windows-Version", _system.WindowsVersion, maskierenErlaubt: false);
        Zeile("Bericht erstellt", _erstelltAm.ToString(Zeitformat, Kultur));
        Zeile("Profil", profil.Name);
        if (!string.IsNullOrWhiteSpace(profil.Beschreibung))
        {
            Zeile("Beschreibung", profil.Beschreibung);
        }

        if (profil.IstDemo)
        {
            Zeile("Betriebsart", "Demo-Modus (simulierte Daten)");
        }

        Zeile("Analysezeitraum", $"{_diagnose.Von.ToString(Zeitformat, Kultur)} bis {_diagnose.Bis.ToString(Zeitformat, Kultur)}");
        Zeile(
            "Maskierung",
            _maskieren
                ? "aktiv (IP-Adressen und Benutzernamen sind unkenntlich gemacht)"
                : "ausgeschaltet: IP-Adressen und Benutzernamen stehen im Klartext");
        _html.AppendLine("</table>");
    }

    private void Zusammenfassung()
    {
        var fehler = _diagnose.Logeintraege.Count(e => e.Schweregrad is Schweregrad.Fehler or Schweregrad.Kritisch);
        var gestoppt = _diagnose.Dienste.Count(d => d.IstKritisch && d.Status == DienstStatus.Gestoppt);
        var fehlgeschlagen = _diagnose.Aufgaben.Count(a => a.IstFehlgeschlagen);

        _html.AppendLine("<h2>Zusammenfassung</h2>");
        _html.AppendLine("<table class=\"info\">");
        Zeile("Fehler im Zeitraum", fehler.ToString(Kultur), hervorheben: fehler > 0);
        Zeile("Fehlergruppen", _diagnose.Fehlergruppen.Count.ToString(Kultur));
        Zeile("Kritische Dienste gestoppt", gestoppt.ToString(Kultur), hervorheben: gestoppt > 0);
        if (_diagnose.Aufgaben.Count > 0)
        {
            Zeile("Aufgaben fehlgeschlagen", fehlgeschlagen.ToString(Kultur), hervorheben: fehlgeschlagen > 0);
        }

        Zeile("Gelesene Logdateien", $"{_diagnose.GeleseneDateien.Count} ({FormatiereGroesse(_diagnose.GeleseneBytes)})");
        _html.AppendLine("</table>");
    }

    private void Dienststatus()
    {
        _html.AppendLine("<h2>Dienststatus</h2>");
        if (_diagnose.Dienste.Count == 0)
        {
            _html.AppendLine("<p class=\"leer\">Keine Dienste im Profil.</p>");
            return;
        }

        _html.AppendLine("<table class=\"daten\">");
        _html.AppendLine("<tr><th>Dienst</th><th>Dienstname</th><th>Status</th><th>Starttyp</th><th>Kritisch</th></tr>");
        foreach (var dienst in _diagnose.Dienste)
        {
            var name = string.IsNullOrWhiteSpace(dienst.Anzeigename) ? dienst.Dienstname : dienst.Anzeigename;
            var rot = dienst.IstKritisch && dienst.Status == DienstStatus.Gestoppt;
            _html.AppendLine(
                $"<tr{(rot ? " class=\"rot\"" : "")}><td>{Text(name)}</td><td>{Text(dienst.Dienstname)}</td>" +
                $"<td>{Text(dienst.Status.Text())}</td><td>{Text(dienst.Starttyp.Text())}</td><td>{(dienst.IstKritisch ? "Ja" : "")}</td></tr>");
        }

        _html.AppendLine("</table>");
    }

    private void Fehleruebersicht()
    {
        _html.AppendLine("<h2>Fehlerübersicht</h2>");
        if (_diagnose.Fehlergruppen.Count == 0)
        {
            _html.AppendLine("<p class=\"leer\">Im Zeitraum wurden keine Fehler gefunden.</p>");
            return;
        }

        _html.AppendLine("<table class=\"daten\">");
        _html.AppendLine("<tr><th>Nr.</th><th>Quelle</th><th>Meldungsmuster</th><th>Anzahl</th><th>Erstes Auftreten</th><th>Letztes Auftreten</th></tr>");
        var nummer = 0;
        foreach (var gruppe in _diagnose.Fehlergruppen.Take(MaxFehlergruppen))
        {
            nummer++;
            _html.AppendLine(
                $"<tr><td>{nummer}</td><td>{Text(gruppe.Quelle)}</td><td>{Text(gruppe.Meldungsmuster)}</td><td class=\"zahl\">{gruppe.Anzahl.ToString(Kultur)}</td>" +
                $"<td>{gruppe.ErstesAuftreten.ToString(Zeitformat, Kultur)}</td><td>{gruppe.LetztesAuftreten.ToString(Zeitformat, Kultur)}</td></tr>");

            // Die jüngste Meldung der Gruppe im Wortlaut (mit Stacktrace) hilft dem Support am meisten.
            if (gruppe.Eintraege.Count > 0)
            {
                var letzte = gruppe.Eintraege[^1];
                _html.AppendLine(
                    $"<tr class=\"detail\"><td></td><td colspan=\"5\"><span class=\"klein\">Letzte Meldung ({letzte.Schweregrad}):</span><pre>{Meldung(letzte.Meldung)}</pre></td></tr>");
            }
        }

        _html.AppendLine("</table>");
        if (_diagnose.Fehlergruppen.Count > MaxFehlergruppen)
        {
            _html.AppendLine(
                $"<p class=\"leer\">Weitere {_diagnose.Fehlergruppen.Count - MaxFehlergruppen} Fehlergruppen sind nicht aufgeführt; alle Einträge stehen in den Logdateien im ZIP.</p>");
        }
    }

    private void Aufgaben()
    {
        // US13: Aufgaben erscheinen nur, wenn das Profil welche enthält.
        if (_diagnose.Aufgaben.Count == 0)
        {
            return;
        }

        _html.AppendLine("<h2>Geplante Aufgaben</h2>");
        _html.AppendLine("<table class=\"daten\">");
        _html.AppendLine("<tr><th>Aufgabe</th><th>Letzter Lauf</th><th>Ergebnis</th><th>Nächster Lauf</th></tr>");
        foreach (var aufgabe in _diagnose.Aufgaben)
        {
            var ergebnis = aufgabe.LetztesErgebnis switch
            {
                null => "noch nicht gelaufen",
                0 => "erfolgreich (0)",
                var code => $"fehlgeschlagen (Code 0x{code:X})",
            };
            _html.AppendLine(
                $"<tr{(aufgabe.IstFehlgeschlagen ? " class=\"rot\"" : "")}><td>{Text(aufgabe.Name)}</td>" +
                $"<td>{Datum(aufgabe.LetzterLauf)}</td><td>{Text(ergebnis)}</td><td>{Datum(aufgabe.NaechsterLauf)}</td></tr>");
        }

        _html.AppendLine("</table>");
    }

    private void Ereignisse()
    {
        // US13: Ereignisse erscheinen nur, wenn die Analyse welche geliefert hat.
        var ereignisse = _diagnose.Logeintraege
            .Where(e => e.Quelle == Logeintrag.EreignisQuelle)
            .OrderByDescending(e => e.Zeitstempel)
            .ToList();
        if (ereignisse.Count == 0)
        {
            return;
        }

        _html.AppendLine("<h2>Ereignisanzeige</h2>");
        _html.AppendLine("<table class=\"daten\">");
        _html.AppendLine("<tr><th>Zeitpunkt</th><th>Schweregrad</th><th>Meldung</th></tr>");
        foreach (var e in ereignisse.Take(MaxEreignisse))
        {
            _html.AppendLine(
                $"<tr><td>{e.Zeitstempel.ToString(Zeitformat, Kultur)}</td><td>{e.Schweregrad}</td><td>{Meldung(e.Meldung)}</td></tr>");
        }

        _html.AppendLine("</table>");
        if (ereignisse.Count > MaxEreignisse)
        {
            _html.AppendLine($"<p class=\"leer\">Es sind die {MaxEreignisse} jüngsten von {ereignisse.Count} Ereignissen aufgeführt.</p>");
        }
    }

    private void UebersprungeneDateien()
    {
        _html.AppendLine("<h2>Übersprungene Dateien</h2>");
        if (_diagnose.UebersprungeneDateien.Count == 0)
        {
            _html.AppendLine("<p class=\"leer\">Es wurde keine Datei übersprungen.</p>");
            return;
        }

        _html.AppendLine("<table class=\"daten\">");
        _html.AppendLine("<tr><th>Datei</th><th>Grund</th></tr>");
        foreach (var datei in _diagnose.UebersprungeneDateien)
        {
            _html.AppendLine($"<tr><td>{Text(datei.Pfad)}</td><td>{Text(datei.Grund)}</td></tr>");
        }

        _html.AppendLine("</table>");
    }

    private void Zeile(string bezeichnung, string wert, bool hervorheben = false, bool maskierenErlaubt = true)
    {
        var inhalt = maskierenErlaubt ? Text(wert) : WebUtility.HtmlEncode(wert);
        _html.AppendLine($"<tr><th>{bezeichnung}</th><td{(hervorheben ? " class=\"rot\"" : "")}>{inhalt}</td></tr>");
    }

    /// <summary>Maskiert (falls aktiv) und kodiert einen Text für die Ausgabe in HTML.</summary>
    private string Text(string text) => WebUtility.HtmlEncode(_maskieren ? _maskiere(text) : text);

    // Mehrzeilige Meldungen bleiben mehrzeilig (in pre); sehr lange Stacktraces werden gekürzt.
    private string Meldung(string meldung)
    {
        var gekuerzt = meldung.Length > MaxMeldungsZeichen
            ? meldung[..MaxMeldungsZeichen] + Environment.NewLine + "… (gekürzt)"
            : meldung;
        return Text(gekuerzt);
    }

    private static string Datum(DateTime? zeitpunkt) =>
        zeitpunkt is { } z ? z.ToString(Zeitformat, Kultur) : "–";

    private static string FormatiereGroesse(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} Byte",
        < 1024 * 1024 => $"{(bytes / 1024.0).ToString("0.#", Kultur)} KB",
        _ => $"{(bytes / (1024.0 * 1024.0)).ToString("0.#", Kultur)} MB",
    };

    // Bewusst einfaches CSS (keine Variablen, kein Grid): Die Vorschau nutzt das WebBrowser-Steuerelement von WPF.
    private const string Stil =
        "body{font-family:'Segoe UI',Arial,sans-serif;font-size:14px;color:#222;margin:24px;max-width:1000px;}" +
        "h1{font-size:24px;margin:0;}" +
        "h2{font-size:17px;margin:26px 0 8px 0;border-bottom:2px solid #1a6fe0;padding-bottom:3px;}" +
        ".untertitel{color:#555;margin:2px 0 8px 0;font-size:15px;}" +
        "table{border-collapse:collapse;width:100%;}" +
        "table.info th{text-align:left;width:220px;padding:4px 8px;color:#444;font-weight:600;border-bottom:1px solid #e4e4e4;}" +
        "table.info td{padding:4px 8px;border-bottom:1px solid #e4e4e4;}" +
        "table.daten th{background:#eef1f5;text-align:left;padding:6px 8px;border:1px solid #d0d5dc;}" +
        "table.daten td{padding:5px 8px;border:1px solid #d0d5dc;vertical-align:top;}" +
        "td.zahl{text-align:right;}" +
        "tr.detail td{background:#fafafa;}" +
        "tr.rot td,td.rot{background:#fde7e7;color:#8e1b1b;font-weight:600;}" +
        "pre{margin:3px 0 0 0;white-space:pre-wrap;word-wrap:break-word;font-family:Consolas,'Courier New',monospace;font-size:12px;}" +
        ".klein{font-size:12px;color:#666;}" +
        ".leer{color:#555;}" +
        ".fuss{margin-top:30px;color:#777;font-size:12px;border-top:1px solid #ddd;padding-top:8px;}" +
        "@media print{body{margin:0;max-width:none;}h2{page-break-after:avoid;}tr{page-break-inside:avoid;}}";
}
