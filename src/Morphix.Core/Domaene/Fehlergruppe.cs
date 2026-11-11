namespace Morphix.Core.Domaene;

/// <summary>Zusammenfassung gleichartiger Fehlermeldungen (GR07: nur Fehler und Kritisch).</summary>
/// <param name="Meldungsmuster">Normalisierte Meldung, bei der variable Teile durch <c>#</c> ersetzt sind.</param>
/// <param name="Quelle">Quelle, aus der die Einträge stammen.</param>
/// <param name="Anzahl">Anzahl der zusammengefassten Einträge.</param>
/// <param name="ErstesAuftreten">Zeitstempel des ältesten Eintrags.</param>
/// <param name="LetztesAuftreten">Zeitstempel des jüngsten Eintrags.</param>
/// <param name="Eintraege">Die zugehörigen Logeinträge in Eingangsreihenfolge.</param>
public sealed record Fehlergruppe(
    string Meldungsmuster,
    string Quelle,
    int Anzahl,
    DateTime ErstesAuftreten,
    DateTime LetztesAuftreten,
    IReadOnlyList<Logeintrag> Eintraege);