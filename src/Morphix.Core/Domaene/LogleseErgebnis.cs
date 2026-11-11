namespace Morphix.Core.Domaene;

/// <summary>Eine Datei, die beim Einlesen übersprungen wurde (NFA07).</summary>
/// <param name="Pfad">Pfad der Datei.</param>
/// <param name="Grund">Deutsche Beschreibung, warum die Datei übersprungen wurde.</param>
public sealed record UebersprungeneDatei(string Pfad, string Grund);

/// <summary>Ergebnis des Einlesens einer Logquelle.</summary>
/// <param name="Eintraege">Alle Einträge im Zeitraum.</param>
/// <param name="Dateianzahl">Anzahl erfolgreich gelesener Dateien.</param>
/// <param name="Bytes">Gesamtgröße der gelesenen Dateien in Byte.</param>
/// <param name="Uebersprungen">Unlesbare oder gesperrte Dateien.</param>
public sealed record LogleseErgebnis(
    IReadOnlyList<Logeintrag> Eintraege,
    int Dateianzahl,
    long Bytes,
    IReadOnlyList<UebersprungeneDatei> Uebersprungen);
