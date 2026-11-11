using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Aufgaben;

/// <summary>
/// Liest die Profilaufgaben aus der Windows-Aufgabenplanung (US10, Adapter hinter <see cref="IAufgabenGateway"/>):
/// letzter Lauf, Ergebnis, nächster Lauf. Der Zugriff ist rein lesend.
/// </summary>
public sealed class WindowsAufgabenGateway : IAufgabenGateway
{
    // Statuscodes der Aufgabenplanung, die kein Ergebnis eines abgeschlossenen Laufs sind (kein Fehler nach GR08).
    private const int NochNichtGelaufen = 0x00041303; // SCHED_S_TASK_HAS_NOT_RUN
    private const int Laeuft = 0x00041301;            // SCHED_S_TASK_RUNNING
    private const int NichtGestartet = 0x00041325;    // SCHED_S_TASK_QUEUED

    private readonly IAufgabenZugriff _zugriff;

    /// <summary>Erzeugt das Gateway.</summary>
    /// <param name="zugriff">Zugriff auf die Aufgabenplanung; ohne Angabe der echte <see cref="TaskSchedulerZugriff"/>.</param>
    public WindowsAufgabenGateway(IAufgabenZugriff? zugriff = null)
    {
        _zugriff = zugriff ?? new TaskSchedulerZugriff();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<GeplanteAufgabe>> AbfragenAsync(IEnumerable<GeplanteAufgabe> aufgaben, CancellationToken abbruch = default)
    {
        var liste = aufgaben.ToList();
        return Task.Run<IReadOnlyList<GeplanteAufgabe>>(() => [.. liste.Select(a => Abfragen(a, abbruch))], abbruch);
    }

    private GeplanteAufgabe Abfragen(GeplanteAufgabe aufgabe, CancellationToken abbruch)
    {
        abbruch.ThrowIfCancellationRequested();
        try
        {
            var zustand = _zugriff.Lesen(aufgabe.Name);
            if (zustand is null)
            {
                return aufgabe;
            }

            // Ohne abgeschlossenen Lauf gibt es kein Ergebnis; sonst würde GR08 eine nie gelaufene Aufgabe als Fehler zählen.
            var hatErgebnis = zustand.LetzterLauf is not null
                && zustand.Ergebniscode is not (NochNichtGelaufen or Laeuft or NichtGestartet);
            return aufgabe with
            {
                LetzterLauf = zustand.LetzterLauf,
                LetztesErgebnis = hatErgebnis ? zustand.Ergebniscode : null,
                NaechsterLauf = zustand.NaechsterLauf,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // NFA07: Eine nicht lesbare Aufgabe (fehlende Rechte, Fehler der Aufgabenplanung) bricht die Analyse
            // nicht ab; sie erscheint ohne Angaben.
            return aufgabe;
        }
    }
}
