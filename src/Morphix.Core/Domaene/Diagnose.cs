namespace Morphix.Core.Domaene;

/// <summary>Ein Analysedurchlauf für ein Systemprofil und einen Zeitraum; Grundlage jedes Support-Berichts.</summary>
public sealed record Diagnose
{
    /// <summary>Das analysierte Profil.</summary>
    public required Systemprofil Profil { get; init; }

    /// <summary>Beginn des Analysezeitraums.</summary>
    public required DateTime Von { get; init; }

    /// <summary>Ende des Analysezeitraums.</summary>
    public required DateTime Bis { get; init; }

    /// <summary>Zeitpunkt, zu dem die Diagnose erstellt wurde.</summary>
    public DateTime ErstelltAm { get; init; }

    /// <summary>Alle gelesenen Logeinträge im Zeitraum.</summary>
    public IReadOnlyList<Logeintrag> Logeintraege { get; init; } = [];

    /// <summary>Fehlergruppen, absteigend nach Anzahl.</summary>
    public IReadOnlyList<Fehlergruppe> Fehlergruppen { get; init; } = [];

    /// <summary>GR09: Ein Bericht kann nur aus einer abgeschlossenen Diagnose erzeugt werden.</summary>
    public bool IstAbgeschlossen { get; init; }
}