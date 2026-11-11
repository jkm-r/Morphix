namespace Morphix.Core.Domaene;

/// <summary>
/// Filter für die Ansicht „Logs und Fehler“ (US04). Alle Kriterien sind optional und werden mit UND verknüpft.
/// </summary>
public static class LogFilter
{
    /// <summary>Längster erlaubter Suchbegriff.</summary>
    public const int MaxSuchbegriffLaenge = 200;

    /// <summary>Meldung bei zu langem Suchbegriff (Wortlaut aus der Spezifikation).</summary>
    public const string SuchbegriffZuLang = "Der Suchbegriff ist zu lang.";

    /// <summary>Prüft den Suchbegriff (optional, höchstens 200 Zeichen).</summary>
    public static Validierungsergebnis PruefeSuchbegriff(string? suchbegriff) =>
        suchbegriff is { Length: > MaxSuchbegriffLaenge }
            ? new Validierungsergebnis([new Validierungsfehler("Suchbegriff", SuchbegriffZuLang)])
            : Validierungsergebnis.Gueltig;

    /// <summary>Wendet die Kriterien auf die Einträge an.</summary>
    /// <param name="eintraege">Die Einträge.</param>
    /// <param name="schweregrad">Nur Einträge mit genau diesem Schweregrad; <c>null</c> = alle.</param>
    /// <param name="suchbegriff">Teilstring in Meldung oder Quelle, ohne Beachtung der Schreibweise; leer = kein Filter.</param>
    /// <param name="vonTag">Nur Einträge ab 00:00 Uhr dieses Tages; <c>null</c> = ohne Untergrenze.</param>
    /// <param name="bisTag">Nur Einträge bis einschließlich dieses Tages; <c>null</c> = ohne Obergrenze.</param>
    /// <remarks>Ein zu langer Suchbegriff (siehe <see cref="PruefeSuchbegriff"/>) wird ignoriert.</remarks>
    public static IEnumerable<Logeintrag> Anwenden(
        IEnumerable<Logeintrag> eintraege,
        Schweregrad? schweregrad = null,
        string? suchbegriff = null,
        DateTime? vonTag = null,
        DateTime? bisTag = null)
    {
        ArgumentNullException.ThrowIfNull(eintraege);

        var ergebnis = eintraege;

        if (schweregrad is { } grad)
        {
            ergebnis = ergebnis.Where(e => e.Schweregrad == grad);
        }

        if (!string.IsNullOrWhiteSpace(suchbegriff) && suchbegriff.Length <= MaxSuchbegriffLaenge)
        {
            var begriff = suchbegriff.Trim();
            ergebnis = ergebnis.Where(e =>
                e.Meldung.Contains(begriff, StringComparison.OrdinalIgnoreCase)
                || e.Quelle.Contains(begriff, StringComparison.OrdinalIgnoreCase));
        }

        if (vonTag is { } von)
        {
            var ab = von.Date;
            ergebnis = ergebnis.Where(e => e.Zeitstempel >= ab);
        }

        if (bisTag is { } bis)
        {
            var vor = bis.Date.AddDays(1);
            ergebnis = ergebnis.Where(e => e.Zeitstempel < vor);
        }

        return ergebnis;
    }
}
