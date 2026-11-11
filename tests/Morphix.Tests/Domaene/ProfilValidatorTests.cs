using Morphix.Core.Domaene;

namespace Morphix.Tests.Domaene;

/// <summary>Tests für GR02 und die Feldregeln (<see cref="ProfilValidator"/>).</summary>
public class ProfilValidatorTests
{
    private static readonly ProfilValidator Validator = new(_ => true);

    private static Systemprofil GueltigesProfil() => new()
    {
        Name = "Musterwerk",
        Logquellen = [new Logquelle { Pfad = @"C:\Logs", Dateimuster = "*.log" }],
        Dienste = [new Dienst { Dienstname = "Muster-Server" }],
    };

    /// <summary>Ein vollständiges Profil ist gültig.</summary>
    [Fact]
    public void Gueltiges_Profil()
    {
        Assert.True(Validator.Pruefen(GueltigesProfil(), []).IstGueltig);
    }

    /// <summary>Der Name muss 3 bis 50 Zeichen lang sein.</summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("ab", false)]
    [InlineData("abc", true)]
    [InlineData("12345678901234567890123456789012345678901234567890", true)]
    [InlineData("123456789012345678901234567890123456789012345678901", false)]
    public void Namenslaenge(string name, bool erwartetGueltig)
    {
        var ergebnis = Validator.Pruefen(GueltigesProfil() with { Name = name }, []);

        Assert.Equal(erwartetGueltig, ergebnis.IstGueltig);
    }

    /// <summary>Der Name muss unter allen Profilen eindeutig sein (ohne Beachtung der Schreibweise).</summary>
    [Fact]
    public void Name_muss_eindeutig_sein()
    {
        var ergebnis = Validator.Pruefen(GueltigesProfil(), ["MUSTERWERK"]);

        Assert.Equal(
            ["Bitte einen eindeutigen Namen mit 3 bis 50 Zeichen eingeben."],
            ergebnis.MeldungenFuer("Name"));
    }

    /// <summary>GR02: mindestens eine Logquelle und mindestens ein Dienst.</summary>
    [Fact]
    public void Logquelle_und_Dienst_sind_Pflicht()
    {
        var ergebnis = Validator.Pruefen(GueltigesProfil() with { Logquellen = [], Dienste = [] }, []);

        Assert.Contains(ergebnis.Fehler, f => f.Feld == "Logquellen");
        Assert.Contains(ergebnis.Fehler, f => f.Feld == "Dienste");
    }

    /// <summary>Ein nicht lesbarer Ordner liefert die Meldung aus der Spezifikation.</summary>
    [Fact]
    public void Ordner_muss_lesbar_sein()
    {
        var validator = new ProfilValidator(_ => false);

        var ergebnis = validator.Pruefen(GueltigesProfil(), []);

        Assert.Equal(
            ["Der Ordner existiert nicht oder ist nicht lesbar."],
            ergebnis.MeldungenFuer("Logquellen[0].Pfad"));
    }

    /// <summary>Das Dateimuster ist Pflicht und darf keine ungültigen Zeichen enthalten.</summary>
    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("*.log", true)]
    [InlineData("app_??.txt", true)]
    [InlineData("a|b", false)]
    [InlineData(@"sub\*.log", false)]
    public void Dateimuster(string muster, bool erwartetGueltig)
    {
        var profil = GueltigesProfil() with { Logquellen = [new Logquelle { Pfad = "x", Dateimuster = muster }] };

        var ergebnis = Validator.Pruefen(profil, []);

        Assert.Equal(erwartetGueltig, ergebnis.IstGueltig);
        if (!erwartetGueltig)
        {
            Assert.Equal(["Bitte ein Dateimuster wie *.log angeben."], ergebnis.MeldungenFuer("Logquellen[0].Dateimuster"));
        }
    }

    /// <summary>Ein Dienst ohne Namen ist ungültig.</summary>
    [Fact]
    public void Dienstname_ist_Pflicht()
    {
        var profil = GueltigesProfil() with { Dienste = [new Dienst { Dienstname = " " }] };

        var ergebnis = Validator.Pruefen(profil, []);

        Assert.Contains(ergebnis.Fehler, f => f.Feld == "Dienste[0].Dienstname");
    }

    /// <summary>Bei der Ereignisanzeige entfallen Ordnerprüfung und Dateimuster; der Protokollname ist Pflicht.</summary>
    [Fact]
    public void Ereignisanzeige_braucht_nur_Protokollnamen()
    {
        var validator = new ProfilValidator(_ => false);
        var ok = GueltigesProfil() with
        {
            Logquellen = [new Logquelle { Typ = LogquellenTyp.Ereignisanzeige, Pfad = "Application", Dateimuster = "" }],
        };
        var leer = GueltigesProfil() with
        {
            Logquellen = [new Logquelle { Typ = LogquellenTyp.Ereignisanzeige, Pfad = "" }],
        };

        Assert.True(validator.Pruefen(ok, []).IstGueltig);
        Assert.False(validator.Pruefen(leer, []).IstGueltig);
    }

    /// <summary>Der Standard-Ordnercheck erkennt vorhandene und fehlende Ordner im Dateisystem.</summary>
    [Fact]
    public void Standardpruefung_nutzt_das_Dateisystem()
    {
        var validator = new ProfilValidator();
        var vorhanden = GueltigesProfil() with { Logquellen = [new Logquelle { Pfad = System.IO.Path.GetTempPath() }] };
        var fehlend = GueltigesProfil() with
        {
            Logquellen = [new Logquelle { Pfad = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N")) }],
        };

        Assert.True(validator.Pruefen(vorhanden, []).IstGueltig);
        Assert.False(validator.Pruefen(fehlend, []).IstGueltig);
    }
}
