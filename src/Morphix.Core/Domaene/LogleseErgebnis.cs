namespace Morphix.Core.Domaene;

/// <summary>Eine Datei, die beim Einlesen übersprungen wurde (NFA07).</summary>
/// <param name="Pfad">Pfad der Datei (bzw. des Ordners, wenn dieser fehlt).</param>
/// <param name="Grund">Deutsche Beschreibung, warum übersprungen wurde.</param>
public sealed record UebersprungeneDatei(string Pfad, string Grund);

/// <summary>Ergebnis des Einlesens einer Logquelle.</summary>
/// <param name="Eintraege">Alle Einträge im Zeitraum.</param>
/// <param name="Dateianzahl">Anzahl erfolgreich gelesener Dateien.</param>
/// <param name="Bytes">Gesamtgröße der gelesenen Dateien in Byte.</param>
/// <param name="Uebersprungen">Unlesbare oder gesperrte Dateien.</param>
/// <param name="GeleseneDateien">Pfade der gelesenen Dateien.</param>
public sealed record LogleseErgebnis(
    IReadOnlyList<Logeintrag> Eintraege,
    int Dateianzahl,
    long Bytes,
    IReadOnlyList<UebersprungeneDatei> Uebersprungen,
    IReadOnlyList<string> GeleseneDateien)
{
    /// <summary>Leeres Ergebnis (nichts gelesen).</summary>
    public static LogleseErgebnis Leer { get; } = new([], 0, 0, [], []);
}
