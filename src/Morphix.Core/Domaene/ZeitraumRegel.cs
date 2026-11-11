namespace Morphix.Core.Domaene;

/// <summary>GR01: Der Analysezeitraum beginnt vor seinem Ende, endet nicht in der Zukunft und umfasst höchstens 30 Tage.</summary>
public static class ZeitraumRegel
{
    /// <summary>Längste erlaubte Dauer eines Analysezeitraums.</summary>
    public static readonly TimeSpan MaximaleDauer = TimeSpan.FromDays(30);

    /// <summary>Meldung bei Verstoß (Wortlaut aus der Spezifikation).</summary>
    public const string Meldung = "Der Zeitraum ist ungültig (max. 30 Tage, nicht in der Zukunft).";

    /// <summary>Feldbezeichner im <see cref="Validierungsergebnis"/>.</summary>
    public const string Feld = "Zeitraum";

    /// <summary>Prüft einen Zeitraum.</summary>
    /// <param name="von">Beginn.</param>
    /// <param name="bis">Ende.</param>
    /// <param name="jetzt">Aktueller Zeitpunkt. Wird übergeben, damit die Regel ohne Systemuhr testbar bleibt.</param>
    public static Validierungsergebnis Pruefen(DateTime von, DateTime bis, DateTime jetzt)
    {
        var gueltig = von < bis && bis <= jetzt && bis - von <= MaximaleDauer;
        return gueltig
            ? Validierungsergebnis.Gueltig
            : new Validierungsergebnis([new Validierungsfehler(Feld, Meldung)]);
    }
}
