namespace Morphix.Core.Domaene;

/// <summary>Aktion, die auf einen Dienst angewendet werden kann.</summary>
public enum DienstAktionsart
{
    /// <summary>Dienst starten.</summary>
    Starten,

    /// <summary>Dienst stoppen.</summary>
    Stoppen,

    /// <summary>Dienst neu starten.</summary>
    Neustarten,
}