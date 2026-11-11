namespace Morphix.Core.Domaene;

/// <summary>Ergebnis einer Prüfung: gültig oder eine Liste von Verstößen.</summary>
public sealed record Validierungsergebnis(IReadOnlyList<Validierungsfehler> Fehler)
{
    /// <summary>Ergebnis ohne Verstöße.</summary>
    public static Validierungsergebnis Gueltig { get; } = new([]);

    /// <summary><c>true</c>, wenn kein Verstoß vorliegt.</summary>
    public bool IstGueltig => Fehler.Count == 0;

    /// <summary>Liefert die Meldungen zu einem Feld.</summary>
    public IEnumerable<string> MeldungenFuer(string feld) =>
        Fehler.Where(f => f.Feld == feld).Select(f => f.Meldung);
}