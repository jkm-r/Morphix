using System.Text.RegularExpressions;

namespace Morphix.Core.Domaene;

/// <summary>
/// Fasst gleichartige Fehler zu Fehlergruppen zusammen (Prozess P3, GR07). Variable Teile einer Meldung
/// (Zahlen, Zeiten, Pfade, …) werden durch <c>#</c> ersetzt, sodass „Timeout bei Terminal 12“ und
/// „Timeout bei Terminal 7“ dasselbe Muster ergeben.
/// </summary>
public static partial class FehlerGruppierer
{
    private const string Platzhalter = "#";

    // Die Reihenfolge ist wichtig: Spezielle Formen zuerst, damit z. B. die Zahlen in einem Datum
    // nicht schon einzeln ersetzt sind, bevor das Datum als Ganzes erkannt wird.
    private static readonly Regex[] Normalisierungen =
    [
        GuidRegex(),
        DatumUhrzeitRegex(),
        WindowsPfadRegex(),
        Ipv4Regex(),
        ZahlRegex(),
    ];

    /// <summary>Ersetzt variable Teile einer Meldung durch <c>#</c>.</summary>
    public static string Normalisiere(string meldung)
    {
        ArgumentNullException.ThrowIfNull(meldung);

        var ergebnis = meldung;
        foreach (var regex in Normalisierungen)
        {
            ergebnis = regex.Replace(ergebnis, Platzhalter);
        }

        return ergebnis;
    }

    /// <summary>
    /// Bildet Fehlergruppen. Berücksichtigt werden nur Einträge mit Schweregrad Fehler oder Kritisch (GR07);
    /// gruppiert wird nach (Quelle, normalisierte erste Meldungszeile). Weitere Zeilen, etwa ein Stacktrace,
    /// bleiben im <see cref="Logeintrag"/> erhalten, fließen aber nicht ins Muster ein. Das Ergebnis ist
    /// absteigend nach Anzahl sortiert, bei Gleichstand nach jüngstem Auftreten, dann nach Muster.
    /// </summary>
    public static IReadOnlyList<Fehlergruppe> Gruppiere(IEnumerable<Logeintrag> eintraege)
    {
        ArgumentNullException.ThrowIfNull(eintraege);

        return eintraege
            .Where(e => e.Schweregrad is Schweregrad.Fehler or Schweregrad.Kritisch)
            .GroupBy(e => (e.Quelle, Muster: Normalisiere(ErsteZeile(e.Meldung))))
            .Select(g => new Fehlergruppe(
                g.Key.Muster,
                g.Key.Quelle,
                g.Count(),
                g.Min(e => e.Zeitstempel),
                g.Max(e => e.Zeitstempel),
                [.. g]))
            .OrderByDescending(g => g.Anzahl)
            .ThenByDescending(g => g.LetztesAuftreten)
            .ThenBy(g => g.Meldungsmuster, StringComparer.Ordinal)
            .ToList();
    }

    private static string ErsteZeile(string meldung)
    {
        var ende = meldung.IndexOf('\n');
        return (ende < 0 ? meldung : meldung[..ende]).TrimEnd('\r');
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}\b")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(
        @"\d{4}-\d{2}-\d{2}(?:[ T]\d{2}:\d{2}:\d{2}(?:[.,]\d+)?)?"
        + @"|\d{1,2}\.\d{1,2}\.\d{4}(?: \d{2}:\d{2}(?::\d{2})?)?"
        + @"|\b\d{1,2}:\d{2}(?::\d{2}(?:[.,]\d+)?)?\b")]
    private static partial Regex DatumUhrzeitRegex();

    // Laufwerkspfad (C:\...) oder UNC-Pfad (\\server\...). Trennzeichen wie Komma, Klammern, Anführungszeichen
    // und ein abschließender Punkt gehören nicht mehr zum Pfad.
    [GeneratedRegex(
        @"(?:[A-Za-z]:\\|\\\\)(?:[^\\/:*?""<>|\s,;'()\[\]]+\\)*(?:[^\\/:*?""<>|\s,;'()\[\]]*[^\\/:*?""<>|\s,;'()\[\].])?")]
    private static partial Regex WindowsPfadRegex();

    [GeneratedRegex(@"\b(?:\d{1,3}\.){3}\d{1,3}\b")]
    private static partial Regex Ipv4Regex();

    // Hexadezimalzahlen (0x80070005) und Dezimalzahlen mit optionalem Dezimalteil.
    [GeneratedRegex(@"0[xX][0-9a-fA-F]+|\d+(?:[.,]\d+)*")]
    private static partial Regex ZahlRegex();
}
