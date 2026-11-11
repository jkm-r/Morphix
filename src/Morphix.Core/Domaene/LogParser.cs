using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Morphix.Core.Domaene;

/// <summary>
/// Zerlegt Logzeilen in <see cref="Logeintrag"/>-Objekte. Zeilen ohne erkennbaren Zeitstempel
/// gehören zum vorherigen Eintrag (GR06).
/// </summary>
public sealed class LogParser
{
    /// <summary>Standardmuster für Zeilen der Form „2026-11-11 10:15:00 [ERROR] Text“.</summary>
    public const string Standardmuster =
        @"^(?<zeit>\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d{1,3})?)\s+\[?(?<stufe>[A-Za-z]+)\]?\s+(?<text>.*)$";

    private readonly Regex _muster;

    /// <summary>Erzeugt einen Parser.</summary>
    /// <param name="zeilenmuster">
    /// Regulärer Ausdruck mit den benannten Gruppen <c>zeit</c>, <c>stufe</c> und <c>text</c>;
    /// <c>null</c> verwendet das <see cref="Standardmuster"/>.
    /// </param>
    /// <exception cref="ArgumentException">Das Muster ist ungültig oder enthält nicht alle drei Gruppen.</exception>
    public LogParser(string? zeilenmuster = null)
    {
        try
        {
            _muster = new Regex(
                string.IsNullOrWhiteSpace(zeilenmuster) ? Standardmuster : zeilenmuster,
                RegexOptions.Compiled | RegexOptions.CultureInvariant,
                TimeSpan.FromSeconds(1));
        }
        catch (RegexParseException ex)
        {
            throw new ArgumentException("Das Zeilenmuster ist kein gültiger regulärer Ausdruck.", nameof(zeilenmuster), ex);
        }

        var gruppen = _muster.GetGroupNames();
        if (!gruppen.Contains("zeit") || !gruppen.Contains("stufe") || !gruppen.Contains("text"))
        {
            throw new ArgumentException("Das Zeilenmuster braucht die Gruppen 'zeit', 'stufe' und 'text'.", nameof(zeilenmuster));
        }
    }

    /// <summary>
    /// Wandelt Zeilen in Logeinträge um. Die Verarbeitung ist lazy, damit sehr große Dateien
    /// nicht zusätzlich als Zeilenliste im Speicher liegen müssen.
    /// </summary>
    /// <param name="zeilen">Die Zeilen einer Logquelle.</param>
    /// <param name="quelle">Quellenname, der in jedem Eintrag steht.</param>
    public IEnumerable<Logeintrag> Parse(IEnumerable<string> zeilen, string quelle)
    {
        ArgumentNullException.ThrowIfNull(zeilen);

        DateTime zeit = default;
        Schweregrad stufe = default;
        StringBuilder? text = null;

        foreach (var zeile in zeilen)
        {
            if (TryParseZeile(zeile, out var z, out var s, out var t))
            {
                if (text is not null)
                {
                    yield return new Logeintrag(zeit, stufe, quelle, text.ToString());
                }

                zeit = z;
                stufe = s;
                text = new StringBuilder(t);
            }
            else if (text is not null)
            {
                // GR06: Fortsetzungszeile (z. B. Stacktrace) gehört zum vorherigen Eintrag.
                text.Append('\n').Append(zeile);
            }
            // Zeilen vor dem ersten Eintrag werden verworfen.
        }

        if (text is not null)
        {
            yield return new Logeintrag(zeit, stufe, quelle, text.ToString());
        }
    }

    /// <summary>
    /// Ordnet eine Stufenbezeichnung einem <see cref="Schweregrad"/> zu (ohne Beachtung der Groß-/Kleinschreibung).
    /// Unbekannte Bezeichnungen ergeben <see cref="Schweregrad.Info"/>.
    /// </summary>
    public static Schweregrad ZuSchweregrad(string? stufe) => stufe?.Trim().ToUpperInvariant() switch
    {
        "WARN" or "WARNING" => Schweregrad.Warnung,
        "ERR" or "ERROR" => Schweregrad.Fehler,
        "FATAL" or "CRIT" or "CRITICAL" => Schweregrad.Kritisch,
        _ => Schweregrad.Info, // TRACE, DEBUG, INFO und alles Unbekannte
    };

    private bool TryParseZeile(string zeile, out DateTime zeit, out Schweregrad stufe, out string text)
    {
        zeit = default;
        stufe = default;
        text = "";

        Match m;
        try
        {
            m = _muster.Match(zeile);
        }
        catch (RegexMatchTimeoutException)
        {
            return false; // Ein pathologisches Benutzermuster darf den Lauf nicht blockieren (NFA07).
        }

        if (!m.Success || !TryParseZeit(m.Groups["zeit"].Value, out zeit))
        {
            return false;
        }

        stufe = ZuSchweregrad(m.Groups["stufe"].Value);
        text = m.Groups["text"].Value;
        return true;
    }

    private static bool TryParseZeit(string wert, out DateTime zeit) =>
        DateTime.TryParse(wert.Replace(',', '.'), CultureInfo.InvariantCulture, DateTimeStyles.None, out zeit);
}
