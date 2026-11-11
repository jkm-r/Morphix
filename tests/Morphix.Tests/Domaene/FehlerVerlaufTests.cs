using Morphix.Core.Domaene;

namespace Morphix.Tests.Domaene;

/// <summary>Tests des <see cref="FehlerVerlauf"/> (US12): Fehler je Stunde bzw. Tag.</summary>
public class FehlerVerlaufTests
{
    private static Logeintrag E(DateTime zeit, Schweregrad grad = Schweregrad.Fehler) => new(zeit, grad, "a.log", "Meldung");

    /// <summary>Bis 48 Stunden wird stündlich gezählt; jede Stunde hat einen Punkt, auch ohne Fehler.</summary>
    [Fact]
    public void Kurzer_Zeitraum_stuendlich_und_lueckenlos()
    {
        var von = new DateTime(2026, 11, 10, 8, 0, 0);
        var verlauf = FehlerVerlauf.Berechne(
            [E(von.AddMinutes(5)), E(von.AddMinutes(50)), E(von.AddHours(2).AddMinutes(10), Schweregrad.Kritisch)],
            von, von.AddHours(3).AddMinutes(59));

        Assert.Equal(Zeitaufloesung.Stunde, verlauf.Aufloesung);
        Assert.Equal(4, verlauf.Punkte.Count);
        Assert.Equal([von, von.AddHours(1), von.AddHours(2), von.AddHours(3)], verlauf.Punkte.Select(p => p.Beginn));
        Assert.Equal([2, 0, 0, 0], verlauf.Punkte.Select(p => p.Fehler));
        Assert.Equal([0, 0, 1, 0], verlauf.Punkte.Select(p => p.Kritisch));
        Assert.Equal(2, verlauf.Maximum);
    }

    /// <summary>Der erste Abschnitt beginnt zur vollen Stunde, auch wenn der Zeitraum mitten in der Stunde beginnt.</summary>
    [Fact]
    public void Erster_Abschnitt_beginnt_zur_vollen_Stunde()
    {
        var von = new DateTime(2026, 11, 10, 8, 30, 0);

        var verlauf = FehlerVerlauf.Berechne([E(von.AddMinutes(1))], von, von.AddHours(1));

        Assert.Equal(new DateTime(2026, 11, 10, 8, 0, 0), verlauf.Punkte[0].Beginn);
        Assert.Equal(2, verlauf.Punkte.Count);
        Assert.Equal(1, verlauf.Punkte[0].Fehler);
    }

    /// <summary>Genau 48 Stunden sind noch stündlich, darüber wird täglich gezählt.</summary>
    [Fact]
    public void Grenze_zwischen_Stunde_und_Tag()
    {
        var von = new DateTime(2026, 11, 1, 0, 0, 0);

        Assert.Equal(Zeitaufloesung.Stunde, FehlerVerlauf.Berechne([], von, von.AddHours(48)).Aufloesung);
        Assert.Equal(Zeitaufloesung.Tag, FehlerVerlauf.Berechne([], von, von.AddHours(48).AddSeconds(1)).Aufloesung);
    }

    /// <summary>Bei Tageswerten (Standardzeitraum: 7 Tage) gibt es einen Punkt je Kalendertag.</summary>
    [Fact]
    public void Sieben_Tage_taeglich()
    {
        var von = new DateTime(2026, 11, 5, 0, 0, 0);
        var bis = new DateTime(2026, 11, 11, 23, 59, 59);
        var verlauf = FehlerVerlauf.Berechne(
            [E(von.AddHours(23).AddMinutes(59)), E(von.AddDays(1)), E(bis), E(bis.AddHours(-1), Schweregrad.Kritisch)],
            von, bis);

        Assert.Equal(Zeitaufloesung.Tag, verlauf.Aufloesung);
        Assert.Equal(7, verlauf.Punkte.Count);
        Assert.Equal([1, 1, 0, 0, 0, 0, 1], verlauf.Punkte.Select(p => p.Fehler));
        Assert.Equal([0, 0, 0, 0, 0, 0, 1], verlauf.Punkte.Select(p => p.Kritisch));
        Assert.Equal(new DateTime(2026, 11, 11), verlauf.Punkte[^1].Beginn);
    }

    /// <summary>Informationen, Warnungen und Einträge außerhalb des Zeitraums werden nicht gezählt.</summary>
    [Fact]
    public void Zaehlt_nur_Fehler_und_Kritisch_im_Zeitraum()
    {
        var von = new DateTime(2026, 11, 10, 8, 0, 0);
        var verlauf = FehlerVerlauf.Berechne(
            [
                E(von.AddMinutes(1), Schweregrad.Info),
                E(von.AddMinutes(2), Schweregrad.Warnung),
                E(von.AddMinutes(-1)),
                E(von.AddHours(5)),
                E(von.AddMinutes(3)),
            ],
            von, von.AddHours(1));

        Assert.Equal(1, verlauf.Punkte.Sum(p => p.Gesamt));
    }

    /// <summary>Ohne Einträge sind alle Punkte null und das Maximum ist 0.</summary>
    [Fact]
    public void Ohne_Eintraege()
    {
        var von = new DateTime(2026, 11, 10, 0, 0, 0);

        var verlauf = FehlerVerlauf.Berechne([], von, von.AddHours(5).AddMinutes(1));

        Assert.Equal(6, verlauf.Punkte.Count);
        Assert.All(verlauf.Punkte, p => Assert.Equal(0, p.Gesamt));
        Assert.Equal(0, verlauf.Maximum);
    }

    /// <summary>Ein umgekehrter Zeitraum ergibt einen leeren Verlauf, null ist ein Programmierfehler.</summary>
    [Fact]
    public void Ungueltige_Eingaben()
    {
        var jetzt = new DateTime(2026, 11, 10);

        Assert.Empty(FehlerVerlauf.Berechne([], jetzt, jetzt.AddHours(-1)).Punkte);
        Assert.Equal(0, FehlerVerlauf.Berechne([], jetzt, jetzt.AddHours(-1)).Maximum);
        Assert.Throws<ArgumentNullException>(() => FehlerVerlauf.Berechne(null!, jetzt, jetzt));
    }

    /// <summary>Auch der 30-Tage-Zeitraum (GR01) bleibt klein: höchstens 30 Tagespunkte.</summary>
    [Fact]
    public void Dreissig_Tage_ergeben_hoechstens_30_Punkte()
    {
        var von = new DateTime(2026, 10, 12, 0, 0, 0);

        var verlauf = FehlerVerlauf.Berechne([], von, von.AddDays(30).AddSeconds(-1));

        Assert.Equal(30, verlauf.Punkte.Count);
    }
}
