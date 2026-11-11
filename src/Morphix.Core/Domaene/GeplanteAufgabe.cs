namespace Morphix.Core.Domaene;

/// <summary>Aufgabe der Windows-Aufgabenplanung, die zu einem Systemprofil gehört.</summary>
public sealed record GeplanteAufgabe
{
    /// <summary>Name der Aufgabe.</summary>
    public string Name { get; init; } = "";

    /// <summary>Zeitpunkt des letzten Laufs, falls bekannt.</summary>
    public DateTime? LetzterLauf { get; init; }

    /// <summary>Ergebniscode des letzten Laufs; 0 bedeutet erfolgreich, <c>null</c> noch nie gelaufen.</summary>
    public int? LetztesErgebnis { get; init; }

    /// <summary>Zeitpunkt des nächsten geplanten Laufs, falls bekannt.</summary>
    public DateTime? NaechsterLauf { get; init; }

    /// <summary>GR08: Eine Aufgabe gilt als fehlgeschlagen, wenn ihr letzter Ergebniscode ungleich 0 ist.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IstFehlgeschlagen => LetztesErgebnis is not null and not 0;
}