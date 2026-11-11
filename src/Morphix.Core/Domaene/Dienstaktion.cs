namespace Morphix.Core.Domaene;

/// <summary>Protokolleintrag einer Start-, Stopp- oder Neustart-Aktion (Could, US16).</summary>
/// <param name="Zeitpunkt">Zeitpunkt der Aktion.</param>
/// <param name="Aktion">Art der Aktion.</param>
/// <param name="Benutzer">Ausführender Benutzer.</param>
/// <param name="Erfolgreich">Gibt an, ob die Aktion erfolgreich war.</param>
public sealed record Dienstaktion(DateTime Zeitpunkt, DienstAktionsart Aktion, string Benutzer, bool Erfolgreich)
{
    /// <summary>Technischer Name des Dienstes, auf den sich die Aktion bezog.</summary>
    public string Dienstname { get; init; } = "";

    /// <summary>Name des Profils, in dem die Aktion ausgeführt wurde.</summary>
    public string Profil { get; init; } = "";

    /// <summary>Ob die Aktion nur simuliert wurde (Demo-Modus, GR04) und kein echter Dienst betroffen war.</summary>
    public bool Simuliert { get; init; }

    /// <summary>Ergebnis in Worten: Erfolgsmeldung oder Grund der Ablehnung bzw. des Fehlschlags.</summary>
    public string Meldung { get; init; } = "";
}
