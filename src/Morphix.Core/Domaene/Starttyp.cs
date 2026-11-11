namespace Morphix.Core.Domaene;

/// <summary>Legt fest, ob und wie ein Windows-Dienst startet.</summary>
public enum Starttyp
{
    /// <summary>Der Starttyp ist unbekannt.</summary>
    Unbekannt,

    /// <summary>Der Dienst startet automatisch.</summary>
    Automatisch,

    /// <summary>Der Dienst wird manuell gestartet.</summary>
    Manuell,

    /// <summary>Der Dienst ist deaktiviert.</summary>
    Deaktiviert,
}