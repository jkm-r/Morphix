namespace Morphix.Core.Domaene;

/// <summary>Ordner mit Logdateien oder ein Protokoll der Ereignisanzeige.</summary>
public sealed record Logquelle
{
    /// <summary>Ordnerpfad (Typ Datei) bzw. Protokollname, z. B. „Application“ (Typ Ereignisanzeige).</summary>
    public string Pfad { get; init; } = "";

    /// <summary>Dateimuster wie <c>*.log</c>; nur für Dateiquellen relevant.</summary>
    public string Dateimuster { get; init; } = "*.log";

    /// <summary>Art der Quelle.</summary>
    public LogquellenTyp Typ { get; init; } = LogquellenTyp.Datei;

    /// <summary>
    /// Optionaler regulärer Ausdruck mit den benannten Gruppen <c>zeit</c>, <c>stufe</c> und <c>text</c>.
    /// Ohne Angabe gilt das Standardmuster des <see cref="LogParser"/>.
    /// </summary>
    public string? Zeilenmuster { get; init; }
}