namespace Morphix.Core.Domaene;

/// <summary>Zustand eines Windows-Dienstes.</summary>
public enum DienstStatus
{
    /// <summary>Der Zustand ist (noch) nicht abgefragt.</summary>
    Unbekannt,

    /// <summary>Der Dienst läuft.</summary>
    Gestartet,

    /// <summary>Der Dienst ist gestoppt.</summary>
    Gestoppt,

    /// <summary>Der Dienst startet gerade.</summary>
    Startet,

    /// <summary>Der Dienst wird gerade gestoppt.</summary>
    Stoppt,

    /// <summary>Der Dienst ist angehalten.</summary>
    Angehalten,

    /// <summary>Der Dienst existiert auf dem System nicht.</summary>
    NichtGefunden,
}