namespace Morphix.Core.Domaene;

/// <summary>Einzelne Ausführung einer geplanten Aufgabe.</summary>
/// <param name="Start">Beginn des Laufs.</param>
/// <param name="Ende">Ende des Laufs.</param>
/// <param name="Ergebniscode">Ergebniscode; 0 bedeutet erfolgreich.</param>
public sealed record Aufgabenlauf(DateTime Start, DateTime Ende, int Ergebniscode);