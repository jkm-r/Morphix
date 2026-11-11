namespace Morphix.Core.Schnittstellen;

/// <summary>Schreibt einen Support-Bericht samt Logdateien in eine ZIP-Datei (US14).</summary>
public interface IBerichtExporter
{
    /// <summary>Prüft, ob in den Ordner der Zieldatei geschrieben werden kann (Eingabevalidierung „Exportordner“).</summary>
    /// <param name="zielpfad">Pfad der ZIP-Datei.</param>
    /// <returns><c>true</c>, wenn der Ordner existiert und beschreibbar ist.</returns>
    bool KannSchreiben(string zielpfad);

    /// <summary>Erzeugt die ZIP-Datei.</summary>
    /// <param name="html">Vollständiger HTML-Bericht (bereits maskiert und kodiert).</param>
    /// <param name="logdateien">Pfade der gelesenen Logdateien; sie werden nur kopiert, nie verändert.</param>
    /// <param name="zielpfad">Pfad der ZIP-Datei.</param>
    /// <param name="zeilenFilter">
    /// Wird, wenn angegeben, auf jede Zeile der Logdateien angewendet (Maskierung, GR10); die Kopie im ZIP
    /// enthält dann das Ergebnis. Ohne Angabe werden die Dateien unverändert kopiert.
    /// </param>
    /// <param name="abbruch">Abbruchsignal.</param>
    Task ExportierenAsync(
        string html,
        IEnumerable<string> logdateien,
        string zielpfad,
        Func<string, string>? zeilenFilter = null,
        CancellationToken abbruch = default);
}
