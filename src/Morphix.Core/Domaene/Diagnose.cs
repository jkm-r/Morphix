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

    /// <summary>Alle gelesenen Logeinträge im Zeitraum, aufsteigend nach Zeitstempel.</summary>
    public IReadOnlyList<Logeintrag> Logeintraege { get; init; } = [];

    /// <summary>Fehlergruppen, absteigend nach Anzahl.</summary>
    public IReadOnlyList<Fehlergruppe> Fehlergruppen { get; init; } = [];

    /// <summary>Dienste des Profils mit dem Zustand zum Zeitpunkt der Analyse.</summary>
    public IReadOnlyList<Dienst> Dienste { get; init; } = [];

    /// <summary>Geplante Aufgaben des Profils mit letztem Lauf und Ergebnis.</summary>
    public IReadOnlyList<GeplanteAufgabe> Aufgaben { get; init; } = [];

    /// <summary>Pfade der erfolgreich gelesenen Logdateien (für den ZIP-Export).</summary>
    public IReadOnlyList<string> GeleseneDateien { get; init; } = [];

    /// <summary>Beim Einlesen übersprungene Dateien (NFA07).</summary>
    public IReadOnlyList<UebersprungeneDatei> UebersprungeneDateien { get; init; } = [];

    /// <summary>Gesamtgröße der gelesenen Dateien in Byte.</summary>
    public long GeleseneBytes { get; init; }

    /// <summary>GR09: Ein Bericht kann nur aus einer abgeschlossenen Diagnose erzeugt werden.</summary>
    public bool IstAbgeschlossen { get; init; }
}
