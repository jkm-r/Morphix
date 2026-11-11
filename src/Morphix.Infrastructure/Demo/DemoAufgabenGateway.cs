using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Demo;

/// <summary>
/// Liefert feste Beispieldaten für die Aufgabenplanung. Die Zeitpunkte liegen relativ zu „jetzt“,
/// damit die Demo zu jedem Zeitpunkt plausibel aussieht.
/// </summary>
public sealed class DemoAufgabenGateway : IAufgabenGateway
{
    private readonly Func<DateTime> _jetzt;

    /// <summary>Erzeugt das Gateway.</summary>
    /// <param name="jetzt">Uhr; Tests übergeben eine feste Zeit.</param>
    public DemoAufgabenGateway(Func<DateTime>? jetzt = null)
    {
        _jetzt = jetzt ?? (() => DateTime.Now);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GeplanteAufgabe>> AbfragenAsync(IEnumerable<GeplanteAufgabe> aufgaben, CancellationToken abbruch = default)
    {
        var heute = _jetzt().Date;
        var daten = new Dictionary<string, (TimeSpan Uhrzeit, int Code)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Nächtliche Datensicherung"] = (TimeSpan.FromHours(2), 0x1), // fehlgeschlagen (GR08)
            ["Zertifikatsprüfung"] = (TimeSpan.FromHours(4), 0),
            ["Protokollbereinigung"] = (TimeSpan.FromHours(23), 0),
        };

        IReadOnlyList<GeplanteAufgabe> ergebnis =
        [
            .. aufgaben.Select(a =>
            {
                if (!daten.TryGetValue(a.Name, out var d))
                {
                    return a;
                }

                // Letzter Lauf: zuletzt vergangener Zeitpunkt zur festen Uhrzeit; nächster Lauf: der folgende.
                var letzter = heute + d.Uhrzeit;
                if (letzter > _jetzt())
                {
                    letzter = letzter.AddDays(-1);
                }

                return a with { LetzterLauf = letzter, LetztesErgebnis = d.Code, NaechsterLauf = letzter.AddDays(1) };
            }),
        ];
        return Task.FromResult(ergebnis);
    }
}
