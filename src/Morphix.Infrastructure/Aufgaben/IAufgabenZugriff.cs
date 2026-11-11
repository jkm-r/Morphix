namespace Morphix.Infrastructure.Aufgaben;

/// <summary>Zustand einer Aufgabe der Aufgabenplanung, wie ihn das System meldet.</summary>
/// <param name="LetzterLauf">Beginn des letzten Laufs; <c>null</c>, wenn die Aufgabe noch nie lief.</param>
/// <param name="Ergebniscode">Rohwert des letzten Ergebnisses (Rückgabecode oder Windows-Statuscode).</param>
/// <param name="NaechsterLauf">Nächster geplanter Lauf; <c>null</c>, wenn keiner geplant ist.</param>
public sealed record AufgabenZustand(DateTime? LetzterLauf, int Ergebniscode, DateTime? NaechsterLauf);

/// <summary>
/// Dünne, <b>rein lesende</b> Schicht über der Windows-Aufgabenplanung. Sie trennt die Logik des
/// <see cref="WindowsAufgabenGateway"/> vom echten Dienst, damit sie ohne echte Aufgaben mit einem Fake getestet wird.
/// </summary>
public interface IAufgabenZugriff
{
    /// <summary>Liest den Zustand der Aufgabe; <c>null</c>, wenn es sie auf dem System nicht gibt.</summary>
    AufgabenZustand? Lesen(string aufgabenname);
}
