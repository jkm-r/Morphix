namespace Morphix.Infrastructure.Ereignisse;

/// <summary>Ein Ereignis der Ereignisanzeige in Rohform.</summary>
/// <param name="Zeit">Zeitpunkt in lokaler Zeit.</param>
/// <param name="Stufe">Windows-Stufe: 1 kritisch, 2 Fehler, 3 Warnung, 4 Information, 5 ausführlich.</param>
/// <param name="Anbieter">Name der Ereignisquelle.</param>
/// <param name="Id">Ereignis-ID.</param>
/// <param name="Beschreibung">Meldungstext; <c>null</c>, wenn Windows keinen liefert.</param>
public sealed record RohEreignis(DateTime Zeit, int Stufe, string Anbieter, int Id, string? Beschreibung);

/// <summary>
/// Dünne, <b>rein lesende</b> Schicht über der Windows-Ereignisanzeige, damit die Logik des
/// <see cref="WindowsEreignisLeser"/> ohne echtes Ereignisprotokoll getestet werden kann.
/// </summary>
public interface IEreignisZugriff
{
    /// <summary>Liest kritische Ereignisse, Fehler und Warnungen eines Protokolls im Zeitraum, jüngste zuerst.</summary>
    /// <param name="protokoll">Name des Protokolls, z. B. „Application“.</param>
    /// <param name="von">Beginn (lokale Zeit).</param>
    /// <param name="bis">Ende (lokale Zeit).</param>
    /// <param name="maximal">Höchstzahl der gelieferten Ereignisse.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    IReadOnlyList<RohEreignis> Lesen(string protokoll, DateTime von, DateTime bis, int maximal, CancellationToken abbruch);
}
