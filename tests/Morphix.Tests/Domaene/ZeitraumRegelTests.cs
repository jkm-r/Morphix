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

    /// <summary>GR01 prüft <c>bis - von &lt; 30 Tage</c>: knapp unter 30 Tagen ist gültig, genau 30 Tage nicht.</summary>
    [Fact]
    public void Grenze_bei_30_Tagen()
    {
        Assert.True(ZeitraumRegel.Pruefen(Jetzt.AddDays(-30).AddSeconds(1), Jetzt, Jetzt).IstGueltig);
        Assert.False(ZeitraumRegel.Pruefen(Jetzt.AddDays(-30), Jetzt, Jetzt).IstGueltig);
        Assert.False(ZeitraumRegel.Pruefen(Jetzt.AddDays(-31), Jetzt, Jetzt).IstGueltig);
    }

    /// <summary>Ein Zeitraum aus 30 ganzen Kalendertagen (00:00:00 bis 23:59:59) ist gültig, 31 Tage nicht.</summary>
    [Fact]
    public void Dreissig_Kalendertage_sind_gueltig_einunddreissig_nicht()
    {
        var jetzt = new DateTime(2026, 11, 11, 12, 0, 0);

        var (von30, bis30) = ZeitraumRegel.AusTagen(new DateTime(2026, 10, 10), new DateTime(2026, 11, 8), jetzt);
        var (von31, bis31) = ZeitraumRegel.AusTagen(new DateTime(2026, 10, 9), new DateTime(2026, 11, 8), jetzt);

        Assert.True(ZeitraumRegel.Pruefen(von30, bis30, jetzt).IstGueltig);
        Assert.False(ZeitraumRegel.Pruefen(von31, bis31, jetzt).IstGueltig);
    }

    /// <summary>„von“ ist 00:00:00, „bis“ ist 23:59:59 des gewählten Tages; Uhrzeiten der Eingabe werden ignoriert.</summary>
    [Fact]
    public void AusTagen_setzt_Tagesgrenzen()
    {
        var (von, bis) = ZeitraumRegel.AusTagen(new DateTime(2026, 11, 1, 15, 30, 0), new DateTime(2026, 11, 5, 8, 0, 0), Jetzt);

        Assert.Equal(new DateTime(2026, 11, 1, 0, 0, 0), von);
        Assert.Equal(new DateTime(2026, 11, 5, 23, 59, 59), bis);
    }

    /// <summary>Liegt das Tagesende nach „jetzt“ (heute gewählt), gilt höchstens „jetzt“.</summary>
    [Fact]
    public void AusTagen_begrenzt_auf_jetzt()
    {
        var (_, bis) = ZeitraumRegel.AusTagen(Jetzt.Date.AddDays(-6), Jetzt.Date, Jetzt);

        Assert.Equal(Jetzt, bis);
        Assert.True(ZeitraumRegel.Pruefen(Jetzt.Date.AddDays(-6), bis, Jetzt).IstGueltig);
    }

    /// <summary>Ein Starttag in der Zukunft ergibt einen ungültigen Zeitraum.</summary>
    [Fact]
    public void AusTagen_Starttag_in_der_Zukunft_ist_ungueltig()
    {
        var (von, bis) = ZeitraumRegel.AusTagen(Jetzt.Date.AddDays(1), Jetzt.Date.AddDays(2), Jetzt);

        Assert.False(ZeitraumRegel.Pruefen(von, bis, Jetzt).IstGueltig);
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
