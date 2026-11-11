using System.Diagnostics.Eventing.Reader;
using System.Globalization;

namespace Morphix.Infrastructure.Ereignisse;

/// <summary>
/// Liest die Ereignisanzeige mit <see cref="EventLogReader"/>. Es wird nur gelesen; Protokolle werden nie
/// geleert, exportiert oder verändert. Dieser Teil ist nicht automatisch getestet (siehe <c>docs/TECHNICAL_DEBT.md</c>).
/// </summary>
public sealed class EventLogZugriff : IEreignisZugriff
{
    private const string UtcFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <inheritdoc />
    public IReadOnlyList<RohEreignis> Lesen(string protokoll, DateTime von, DateTime bis, int maximal, CancellationToken abbruch)
    {
        // Level 1 = kritisch, 2 = Fehler, 3 = Warnung. SystemTime ist in der Abfrage UTC.
        var vonUtc = von.ToUniversalTime().ToString(UtcFormat, CultureInfo.InvariantCulture);
        var bisUtc = bis.ToUniversalTime().ToString(UtcFormat, CultureInfo.InvariantCulture);
        var abfrage = new EventLogQuery(
            protokoll,
            PathType.LogName,
            $"*[System[(Level=1 or Level=2 or Level=3) and TimeCreated[@SystemTime>='{vonUtc}' and @SystemTime<='{bisUtc}']]]")
        {
            ReverseDirection = true, // jüngste zuerst, damit bei Überschreitung der Höchstzahl die ältesten fehlen
        };

        var ergebnis = new List<RohEreignis>();
        using var leser = new EventLogReader(abfrage);
        while (ergebnis.Count < maximal)
        {
            abbruch.ThrowIfCancellationRequested();
            using var ereignis = leser.ReadEvent();
            if (ereignis is null)
            {
                break;
            }

            ergebnis.Add(new RohEreignis(
                ereignis.TimeCreated ?? DateTime.MinValue,
                ereignis.Level ?? 0,
                ereignis.ProviderName ?? "",
                ereignis.Id,
                Beschreibung(ereignis)));
        }

        return ergebnis;
    }

    private static string? Beschreibung(EventRecord ereignis)
    {
        try
        {
            return ereignis.FormatDescription();
        }
        catch (EventLogException)
        {
            // Die Meldungsdatei des Anbieters fehlt; das Ereignis bleibt trotzdem sichtbar.
            return null;
        }
    }
}
