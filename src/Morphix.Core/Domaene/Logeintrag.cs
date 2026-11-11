namespace Morphix.Core.Domaene;

/// <summary>Einzelne Meldung aus einer Logquelle. Mehrzeilige Meldungen sind mit <c>\n</c> verbunden.</summary>
/// <param name="Zeitstempel">Zeitpunkt der Meldung.</param>
/// <param name="Schweregrad">Einstufung der Meldung.</param>
/// <param name="Quelle">Herkunft, z. B. Dateiname oder Protokollname.</param>
/// <param name="Meldung">Text der Meldung.</param>
public sealed record Logeintrag(DateTime Zeitstempel, Schweregrad Schweregrad, string Quelle, string Meldung)
{
    /// <summary>Wert von <see cref="Quelle"/> für Einträge der Windows-Ereignisanzeige; der Bericht führt sie gesondert auf.</summary>
    public const string EreignisQuelle = "Ereignisanzeige";
}