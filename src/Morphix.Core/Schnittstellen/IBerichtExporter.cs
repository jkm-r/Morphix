namespace Morphix.Core.Schnittstellen;

/// <summary>Schreibt einen Support-Bericht samt Logdateien in eine ZIP-Datei (US14).</summary>
public interface IBerichtExporter
{
    /// <summary>Erzeugt die ZIP-Datei.</summary>
    /// <param name="html">Vollständiger HTML-Bericht (bereits maskiert und kodiert).</param>
    /// <param name="logdateien">Pfade der gelesenen Logdateien; sie werden nur kopiert, nie verändert.</param>
    /// <param name="zielpfad">Pfad der ZIP-Datei.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    Task ExportierenAsync(string html, IEnumerable<string> logdateien, string zielpfad, CancellationToken abbruch = default);
}
