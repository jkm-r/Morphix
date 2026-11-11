namespace Morphix.Core.Domaene;

/// <summary>Auflösung des Fehlerverlaufs: ein Zeitabschnitt ist eine Stunde oder ein Tag.</summary>
public enum Zeitaufloesung
{
    /// <summary>Ein Wert pro Stunde (bei Zeiträumen bis 48 Stunden).</summary>
    Stunde,

    /// <summary>Ein Wert pro Kalendertag (bei längeren Zeiträumen).</summary>
    Tag,
}

/// <summary>Anzahl der Fehler in einem Zeitabschnitt des Verlaufs.</summary>
/// <param name="Beginn">Beginn des Abschnitts (volle Stunde bzw. 00:00 Uhr des Tages).</param>
/// <param name="Fehler">Anzahl der Einträge mit Schweregrad Fehler.</param>
/// <param name="Kritisch">Anzahl der Einträge mit Schweregrad Kritisch.</param>
public sealed record VerlaufPunkt(DateTime Beginn, int Fehler, int Kritisch)
{
    /// <summary>Summe aus Fehlern und kritischen Einträgen.</summary>
    public int Gesamt => Fehler + Kritisch;
}

/// <summary>
/// Fehlerhäufigkeit über die Zeit (Could, US12): Anzahl der Einträge mit Schweregrad Fehler oder Kritisch je Stunde
/// (Zeitraum bis 48 Stunden) bzw. je Tag. Abschnitte ohne Fehler sind enthalten (Anzahl 0), damit das Diagramm
/// lückenlos ist.
/// </summary>
/// <param name="Aufloesung">Stunde oder Tag.</param>
/// <param name="Punkte">Die Abschnitte in zeitlicher Reihenfolge.</param>
public sealed record FehlerVerlauf(Zeitaufloesung Aufloesung, IReadOnlyList<VerlaufPunkt> Punkte)
{
    /// <summary>Zeiträume bis zu dieser Dauer werden stündlich dargestellt, längere täglich.</summary>
    public static readonly TimeSpan MaxStundenZeitraum = TimeSpan.FromHours(48);

    /// <summary>Höchste Fehlerzahl eines Abschnitts (Summe aus Fehlern und kritischen Einträgen).</summary>
    public int Maximum => Punkte.Count == 0 ? 0 : Punkte.Max(p => p.Gesamt);

    /// <summary>Berechnet den Verlauf.</summary>
    /// <param name="eintraege">Alle Einträge; Informationen und Warnungen werden nicht gezählt.</param>
    /// <param name="von">Beginn des Zeitraums.</param>
    /// <param name="bis">Ende des Zeitraums (einschließlich).</param>
    public static FehlerVerlauf Berechne(IEnumerable<Logeintrag> eintraege, DateTime von, DateTime bis)
    {
        ArgumentNullException.ThrowIfNull(eintraege);

        var aufloesung = bis - von <= MaxStundenZeitraum ? Zeitaufloesung.Stunde : Zeitaufloesung.Tag;
        if (bis < von)
        {
            return new FehlerVerlauf(aufloesung, []);
        }

        var schritt = aufloesung == Zeitaufloesung.Stunde ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var start = aufloesung == Zeitaufloesung.Stunde
            ? new DateTime(von.Year, von.Month, von.Day, von.Hour, 0, 0, von.Kind)
            : von.Date;
        var anzahl = (int)((bis - start).Ticks / schritt.Ticks) + 1;

        var fehler = new int[anzahl];
        var kritisch = new int[anzahl];
        foreach (var e in eintraege)
        {
            if (e.Schweregrad is not (Schweregrad.Fehler or Schweregrad.Kritisch) || e.Zeitstempel < von || e.Zeitstempel > bis)
            {
                continue;
            }

            var index = (int)((e.Zeitstempel - start).Ticks / schritt.Ticks);
            if (e.Schweregrad == Schweregrad.Fehler)
            {
                fehler[index]++;
            }
            else
            {
                kritisch[index]++;
            }
        }

        return new FehlerVerlauf(
            aufloesung,
            [.. Enumerable.Range(0, anzahl).Select(i => new VerlaufPunkt(start + i * schritt, fehler[i], kritisch[i]))]);
    }
}
