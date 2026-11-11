using Microsoft.Win32.TaskScheduler;

namespace Morphix.Infrastructure.Aufgaben;

/// <summary>
/// Liest Aufgaben mit der Bibliothek TaskScheduler. Es werden ausschließlich Eigenschaften gelesen; Aufgaben werden
/// weder gestartet noch beendet, geändert oder gelöscht. Dieser Teil ist nicht automatisch getestet (siehe
/// <c>docs/TECHNICAL_DEBT.md</c>), weil Tests keine echten Aufgaben anfassen.
/// </summary>
public sealed class TaskSchedulerZugriff : IAufgabenZugriff
{
    /// <inheritdoc />
    public AufgabenZustand? Lesen(string aufgabenname)
    {
        using var dienst = new TaskService();

        // Suche in allen Ordnern der Aufgabenplanung, damit der einfache Name genügt.
        using var aufgabe = dienst.FindTask(aufgabenname, searchAllFolders: true);
        if (aufgabe is null)
        {
            return null;
        }

        return new AufgabenZustand(
            aufgabe.LastRunTime == DateTime.MinValue ? null : aufgabe.LastRunTime,
            aufgabe.LastTaskResult,
            aufgabe.NextRunTime == DateTime.MinValue ? null : aufgabe.NextRunTime);
    }
}
