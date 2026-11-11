using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Ereignisse;

/// <summary>
/// Liest Warnungen, Fehler und kritische Ereignisse eines Protokolls der Ereignisanzeige (US06). Die Ereignisse
/// erscheinen als <see cref="Logeintrag"/> mit der Quelle <see cref="Logeintrag.EreignisQuelle"/> in derselben
/// Liste wie die Logdateien. Der Zugriff ist rein lesend.
/// </summary>
public sealed class WindowsEreignisLeser : IEreignisLeser
{
    /// <summary>Höchstzahl gelesener Ereignisse je Protokoll und Analyse; bei mehr fehlen die ältesten.</summary>
    public const int MaxEreignisse = 5000;

    private readonly IEreignisZugriff _zugriff;

    /// <summary>Erzeugt den Leser.</summary>
    /// <param name="zugriff">Zugriff auf die Ereignisanzeige; ohne Angabe der echte <see cref="EventLogZugriff"/>.</param>
    public WindowsEreignisLeser(IEreignisZugriff? zugriff = null)
    {
        _zugriff = zugriff ?? new EventLogZugriff();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Ein nicht lesbares Protokoll (unbekannter Name, fehlende Rechte) löst eine Ausnahme aus; der
    /// <c>DiagnoseService</c> meldet es als übersprungene Quelle (NFA07).
    /// </remarks>
    public Task<IReadOnlyList<Logeintrag>> LesenAsync(Logquelle quelle, DateTime von, DateTime bis, CancellationToken abbruch = default) =>
        Task.Run<IReadOnlyList<Logeintrag>>(
            () =>
            [
                .. _zugriff.Lesen(quelle.Pfad, von, bis, MaxEreignisse, abbruch)
                    .Where(e => e.Zeit >= von && e.Zeit <= bis)
                    .Select(Abbilden)
                    .OfType<Logeintrag>()
                    .OrderBy(e => e.Zeitstempel),
            ],
            abbruch);

    private static Logeintrag? Abbilden(RohEreignis e)
    {
        var grad = e.Stufe switch
        {
            1 => Schweregrad.Kritisch,
            2 => Schweregrad.Fehler,
            3 => Schweregrad.Warnung,
            _ => (Schweregrad?)null, // Information und ausführliche Ereignisse gehören nicht zu US06
        };
        if (grad is null)
        {
            return null;
        }

        var text = string.IsNullOrWhiteSpace(e.Beschreibung) ? "(keine Beschreibung verfügbar)" : e.Beschreibung.Trim();
        return new Logeintrag(e.Zeit, grad.Value, Logeintrag.EreignisQuelle, $"{e.Anbieter} (ID {e.Id}): {text}");
    }
}
