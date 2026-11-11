namespace Morphix.Core.Domaene;

/// <summary>Windows-Dienst, der zu einem Systemprofil gehört.</summary>
public sealed record Dienst
{
    /// <summary>Technischer Name des Dienstes.</summary>
    public string Dienstname { get; init; } = "";

    /// <summary>Anzeigename; leer, solange er nicht vom System gelesen wurde.</summary>
    public string Anzeigename { get; init; } = "";

    /// <summary>Aktueller Zustand (Laufzeitwert, wird nicht im Profil gespeichert).</summary>
    public DienstStatus Status { get; init; } = DienstStatus.Unbekannt;

    /// <summary>Starttyp (Laufzeitwert, wird nicht im Profil gespeichert).</summary>
    public Starttyp Starttyp { get; init; } = Starttyp.Unbekannt;

    /// <summary>Kennzeichnet einen Dienst, ohne den das überwachte System nicht funktioniert.</summary>
    public bool IstKritisch { get; init; }
}