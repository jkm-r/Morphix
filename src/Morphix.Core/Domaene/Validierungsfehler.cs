namespace Morphix.Core.Domaene;

/// <summary>Ein Verstoß gegen eine Eingaberegel, bezogen auf ein Feld.</summary>
/// <param name="Feld">Bezeichner des Feldes, z. B. <c>Name</c> oder <c>Logquellen[0].Pfad</c>.</param>
/// <param name="Meldung">Deutsche Meldung für den Anwender.</param>
public sealed record Validierungsfehler(string Feld, string Meldung);