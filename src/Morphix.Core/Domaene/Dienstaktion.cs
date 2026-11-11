namespace Morphix.Core.Domaene;

/// <summary>Protokolleintrag einer Start-, Stopp- oder Neustart-Aktion (Could, US16).</summary>
/// <param name="Zeitpunkt">Zeitpunkt der Aktion.</param>
/// <param name="Aktion">Art der Aktion.</param>
/// <param name="Benutzer">Ausführender Benutzer.</param>
/// <param name="Erfolgreich">Gibt an, ob die Aktion erfolgreich war.</param>
public sealed record Dienstaktion(DateTime Zeitpunkt, DienstAktionsart Aktion, string Benutzer, bool Erfolgreich);