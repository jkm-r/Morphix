namespace Morphix.Core.Domaene;

/// <summary>Einstufung eines Logeintrags. Die Reihenfolge entspricht der Schwere (aufsteigend).</summary>
public enum Schweregrad
{
    /// <summary>Information oder Debug-Ausgabe.</summary>
    Info,

    /// <summary>Warnung.</summary>
    Warnung,

    /// <summary>Fehler.</summary>
    Fehler,

    /// <summary>Kritischer Fehler.</summary>
    Kritisch,
}