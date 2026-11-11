using Morphix.Core.Domaene;

namespace Morphix.Tests.Domaene;

/// <summary>Tests für GR01 (<see cref="ZeitraumRegel"/>).</summary>
public class ZeitraumRegelTests
{
    private static readonly DateTime Jetzt = new(2026, 11, 11, 12, 0, 0);

    /// <summary>Ein normaler Zeitraum in der Vergangenheit ist gültig.</summary>
    [Fact]
    public void Gueltiger_Zeitraum()
    {
        var ergebnis = ZeitraumRegel.Pruefen(Jetzt.AddDays(-7), Jetzt.AddDays(-1), Jetzt);

        Assert.True(ergebnis.IstGueltig);
    }

    /// <summary>Genau 30 Tage sind noch erlaubt.</summary>
    [Fact]
    public void Genau_30_Tage_sind_gueltig()
    {
        Assert.True(ZeitraumRegel.Pruefen(Jetzt.AddDays(-30), Jetzt, Jetzt).IstGueltig);
    }

    /// <summary>Eine Sekunde mehr als 30 Tage ist ungültig.</summary>
    [Fact]
    public void Mehr_als_30_Tage_sind_ungueltig()
    {
        Assert.False(ZeitraumRegel.Pruefen(Jetzt.AddDays(-30).AddSeconds(-1), Jetzt, Jetzt).IstGueltig);
    }

    /// <summary>Ein Ende in der Zukunft ist ungültig; genau „jetzt“ ist erlaubt.</summary>
    [Fact]
    public void Ende_in_der_Zukunft_ist_ungueltig()
    {
        Assert.False(ZeitraumRegel.Pruefen(Jetzt.AddDays(-1), Jetzt.AddSeconds(1), Jetzt).IstGueltig);
        Assert.True(ZeitraumRegel.Pruefen(Jetzt.AddDays(-1), Jetzt, Jetzt).IstGueltig);
    }

    /// <summary>Von muss echt vor bis liegen (gleich oder vertauscht ist ungültig).</summary>
    [Fact]
    public void Von_nicht_vor_bis_ist_ungueltig()
    {
        Assert.False(ZeitraumRegel.Pruefen(Jetzt.AddDays(-1), Jetzt.AddDays(-1), Jetzt).IstGueltig);
        Assert.False(ZeitraumRegel.Pruefen(Jetzt.AddDays(-1), Jetzt.AddDays(-2), Jetzt).IstGueltig);
    }

    /// <summary>Die Meldung entspricht dem Wortlaut der Spezifikation.</summary>
    [Fact]
    public void Meldung_entspricht_der_Spezifikation()
    {
        var ergebnis = ZeitraumRegel.Pruefen(Jetzt, Jetzt, Jetzt);

        Assert.Equal(
            ["Der Zeitraum ist ungültig (max. 30 Tage, nicht in der Zukunft)."],
            ergebnis.MeldungenFuer("Zeitraum"));
    }
}
