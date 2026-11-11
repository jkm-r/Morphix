namespace Morphix.Core.Domaene;

/// <summary>Exportiertes Ergebnis einer Diagnose.</summary>
/// <param name="ErstelltAm">Zeitpunkt der Erstellung.</param>
/// <param name="Maskiert">Gibt an, ob sensible Daten maskiert wurden (GR10).</param>
/// <param name="Dateipfad">Pfad der exportierten Datei; <c>null</c>, solange nicht exportiert.</param>
public sealed record SupportBericht(DateTime ErstelltAm, bool Maskiert, string? Dateipfad);