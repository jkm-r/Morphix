using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Demo;

/// <summary>Liefert feste Beispielereignisse der Ereignisanzeige, relativ zu „jetzt“.</summary>
public sealed class DemoEreignisLeser : IEreignisLeser
{
    private readonly Func<DateTime> _jetzt;

    /// <summary>Erzeugt den Leser.</summary>
    /// <param name="jetzt">Uhr; Tests übergeben eine feste Zeit.</param>
    public DemoEreignisLeser(Func<DateTime>? jetzt = null)
    {
        _jetzt = jetzt ?? (() => DateTime.Now);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Logeintrag>> LesenAsync(Logquelle quelle, DateTime von, DateTime bis, CancellationToken abbruch = default)
    {
        var jetzt = _jetzt();
        IReadOnlyList<Logeintrag> beispiele =
        [
            new(jetzt.AddHours(-3), Schweregrad.Fehler, "Ereignisanzeige", "Dienst Muster-Kommunikationsdienst wurde unerwartet beendet."),
            new(jetzt.AddHours(-26), Schweregrad.Warnung, "Ereignisanzeige", "Das Zeitsynchronisierungsintervall wurde überschritten."),
            new(jetzt.AddDays(-2), Schweregrad.Fehler, "Ereignisanzeige", "Die Anwendung Muster.Server.exe reagiert nicht mehr."),
            new(jetzt.AddDays(-5), Schweregrad.Kritisch, "Ereignisanzeige", "Das System wurde unerwartet heruntergefahren."),
        ];

        IReadOnlyList<Logeintrag> imZeitraum = [.. beispiele.Where(e => e.Zeitstempel >= von && e.Zeitstempel <= bis)];
        return Task.FromResult(imZeitraum);
    }
}
