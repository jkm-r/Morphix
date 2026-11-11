namespace Morphix.Core.Domaene;

/// <summary>
/// GR01: Der Analysezeitraum beginnt vor seinem Ende, endet nicht in der Zukunft und umfasst höchstens 30 Tage.
/// Die Oberfläche wählt ganze Tage (siehe <see cref="AusTagen"/>); „höchstens 30 Tage“ bedeutet dann
/// <c>bis - von &lt; 30 Tage</c>, weil der letzte Tag um 23:59:59 endet.
/// </summary>
public static class ZeitraumRegel
{
    /// <summary>Grenze der Dauer: ein Zeitraum muss kürzer als 30 Tage sein.</summary>
    public static readonly TimeSpan MaximaleDauer = TimeSpan.FromDays(30);

    /// <summary>Meldung bei Verstoß (Wortlaut aus der Spezifikation).</summary>
    public const string Meldung = "Der Zeitraum ist ungültig (max. 30 Tage, nicht in der Zukunft).";

    /// <summary>Feldbezeichner im <see cref="Validierungsergebnis"/>.</summary>
    public const string Feld = "Zeitraum";

    /// <summary>
    /// Wandelt zwei ausgewählte Kalendertage in einen Zeitraum um: „von“ ist 00:00:00 des ersten Tages,
    /// „bis“ ist 23:59:59 des letzten Tages, höchstens aber <paramref name="jetzt"/>.
    /// </summary>
    /// <param name="vonTag">Erster Tag (die Uhrzeit wird ignoriert).</param>
    /// <param name="bisTag">Letzter Tag (die Uhrzeit wird ignoriert).</param>
    /// <param name="jetzt">Aktueller Zeitpunkt.</param>
    public static (DateTime Von, DateTime Bis) AusTagen(DateTime vonTag, DateTime bisTag, DateTime jetzt)
    {
        var von = vonTag.Date;
        var bis = bisTag.Date.AddDays(1).AddSeconds(-1);
        return (von, bis > jetzt ? jetzt : bis);
    }

    /// <summary>Prüft einen Zeitraum.</summary>
    /// <param name="von">Beginn.</param>
    /// <param name="bis">Ende.</param>
    /// <param name="jetzt">Aktueller Zeitpunkt. Wird übergeben, damit die Regel ohne Systemuhr testbar bleibt.</param>
    public static Validierungsergebnis Pruefen(DateTime von, DateTime bis, DateTime jetzt)
    {
        var gueltig = von < bis && bis <= jetzt && bis - von < MaximaleDauer;
        return gueltig
            ? Validierungsergebnis.Gueltig
            : new Validierungsergebnis([new Validierungsfehler(Feld, Meldung)]);
    }
}
