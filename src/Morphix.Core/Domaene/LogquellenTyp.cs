namespace Morphix.Core.Domaene;

/// <summary>Art einer Logquelle.</summary>
public enum LogquellenTyp
{
    /// <summary>Ordner mit Logdateien.</summary>
    Datei,

    /// <summary>Protokoll der Windows-Ereignisanzeige.</summary>
    Ereignisanzeige,
}