namespace Morphix.Core.Domaene;

/// <summary>Deutsche Anzeigetexte für Aufzählungen. Oberfläche und Bericht verwenden dieselben Texte.</summary>
public static class AnzeigeTexte
{
    /// <summary>Dienststatus als deutscher Text.</summary>
    public static string Text(this DienstStatus status) => status switch
    {
        DienstStatus.Gestartet => "Gestartet",
        DienstStatus.Gestoppt => "Gestoppt",
        DienstStatus.Startet => "Startet …",
        DienstStatus.Stoppt => "Stoppt …",
        DienstStatus.Angehalten => "Angehalten",
        DienstStatus.NichtGefunden => "Nicht gefunden",
        _ => "Unbekannt",
    };

    /// <summary>Starttyp als deutscher Text; leer, wenn unbekannt.</summary>
    public static string Text(this Starttyp starttyp) => starttyp switch
    {
        Starttyp.Automatisch => "Automatisch",
        Starttyp.Manuell => "Manuell",
        Starttyp.Deaktiviert => "Deaktiviert",
        _ => "",
    };
}
