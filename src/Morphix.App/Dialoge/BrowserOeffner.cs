using System.Diagnostics;
using System.IO;
using System.Text;

namespace Morphix.App.Dialoge;

/// <summary>Zeigt einen HTML-Bericht im Standardbrowser. Erlaubt Tests, ohne einen Browser zu starten.</summary>
public interface IBrowserOeffner
{
    /// <summary>Schreibt den HTML-Text in eine temporäre Datei und öffnet sie im Standardbrowser.</summary>
    /// <param name="html">Der vollständige Bericht (so, wie er angezeigt werden soll, also ggf. maskiert).</param>
    /// <returns>Pfad der temporären Datei.</returns>
    /// <exception cref="IOException">Die Datei konnte nicht geschrieben werden.</exception>
    string ZeigeHtml(string html);
}

/// <summary>
/// Öffnet den Bericht als lokale Datei im Standardbrowser. Es wird nur eine Datei geöffnet, es gibt keinen
/// Netzwerkzugriff durch Morphix (NFA06). Die Dateien liegen unter <c>%TEMP%\Morphix</c>; ältere werden beim
/// nächsten Öffnen gelöscht, weil sie bei ausgeschalteter Maskierung sensible Daten enthalten können.
/// </summary>
public sealed class BrowserOeffner : IBrowserOeffner
{
    /// <summary>Temporäre Berichte werden nach dieser Zeit beim nächsten Öffnen entfernt.</summary>
    public static readonly TimeSpan Aufbewahrung = TimeSpan.FromHours(1);

    private readonly string _ordner;
    private readonly Action<string> _starter;

    /// <summary>Erzeugt den Öffner.</summary>
    /// <param name="ordner">Ordner für die temporären Dateien; ohne Angabe <c>%TEMP%\Morphix</c>.</param>
    /// <param name="starter">Öffnet die Datei; ohne Angabe der Standardbrowser. Tests übergeben einen Fake.</param>
    public BrowserOeffner(string? ordner = null, Action<string>? starter = null)
    {
        _ordner = ordner ?? Path.Combine(Path.GetTempPath(), "Morphix");
        _starter = starter ?? StarteStandardbrowser;
    }

    /// <inheritdoc />
    public string ZeigeHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        Directory.CreateDirectory(_ordner);
        RaeumeAuf();

        var pfad = Path.Combine(_ordner, $"Morphix_Bericht_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.html");
        File.WriteAllText(pfad, html, new UTF8Encoding(false));
        _starter(pfad);
        return pfad;
    }

    private void RaeumeAuf()
    {
        foreach (var datei in Directory.EnumerateFiles(_ordner, "Morphix_Bericht_*.html"))
        {
            try
            {
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(datei) > Aufbewahrung)
                {
                    File.Delete(datei);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Eine Datei, die gerade im Browser offen ist, bleibt liegen; das ist unkritisch.
            }
        }
    }

    private static void StarteStandardbrowser(string pfad)
    {
        // Die Shell öffnet .html mit dem Standardprogramm.
        using var prozess = Process.Start(new ProcessStartInfo(pfad) { UseShellExecute = true });
    }
}
