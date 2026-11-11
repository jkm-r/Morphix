namespace Morphix.Core.Domaene;

/// <summary>Beschreibt ein überwachtes System: welche Logquellen, Dienste und Aufgaben dazugehören.</summary>
public sealed record Systemprofil
{
    /// <summary>Eindeutiger Name (3 bis 50 Zeichen).</summary>
    public string Name { get; init; } = "";

    /// <summary>Freie Beschreibung.</summary>
    public string Beschreibung { get; init; } = "";

    /// <summary>Wenn <c>true</c>, werden statt der Windows-Schnittstellen simulierte Demo-Datenquellen verwendet.</summary>
    public bool IstDemo { get; init; }

    /// <summary>Logquellen des Systems (mindestens eine, GR02).</summary>
    public List<Logquelle> Logquellen { get; init; } = [];

    /// <summary>Dienste des Systems (mindestens einer, GR02).</summary>
    public List<Dienst> Dienste { get; init; } = [];

    /// <summary>Geplante Aufgaben des Systems (beliebig viele).</summary>
    public List<GeplanteAufgabe> Aufgaben { get; init; } = [];
}