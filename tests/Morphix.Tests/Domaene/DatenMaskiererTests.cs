using Morphix.Core.Domaene;

namespace Morphix.Tests.Domaene;

/// <summary>Tests für den <see cref="DatenMaskierer"/> (US15, GR10).</summary>
public class DatenMaskiererTests
{
    private static readonly DatenMaskierer Maskierer = new("");

    /// <summary>IPv4-Adressen werden vollständig ersetzt.</summary>
    [Theory]
    [InlineData("Verbindung zu 192.168.10.25 verloren", "Verbindung zu ***.***.***.*** verloren")]
    [InlineData("von 10.0.0.1 nach 10.0.0.2", "von ***.***.***.*** nach ***.***.***.***")]
    [InlineData("Client 10.0.0.1:8080", "Client ***.***.***.***:8080")]
    [InlineData("Keine IP 999.1.1.1 und Version 1.2.3", "Keine IP 999.1.1.1 und Version 1.2.3")]
    public void IpAdressen(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, Maskierer.Maskiere(eingabe));
    }

    /// <summary>Benutzernamen in den drei im Plan genannten Formen.</summary>
    [Theory]
    [InlineData(@"Anmeldung von MUSTERWERK\anna.beispiel erfolgreich", "Anmeldung von [Benutzer] erfolgreich")]
    [InlineData("Login user=mmustermann, Sitzung 5", "Login user=[Benutzer], Sitzung 5")]
    [InlineData("USER=mmustermann", "USER=[Benutzer]")]
    [InlineData("Benutzer: tmuster gesperrt", "Benutzer: [Benutzer] gesperrt")]
    [InlineData("Benutzer:tmuster", "Benutzer:[Benutzer]")]
    public void Benutzernamen(string eingabe, string erwartet)
    {
        Assert.Equal(erwartet, Maskierer.Maskiere(eingabe));
    }

    /// <summary>Pfade sind keine Benutzernamen in Domänenform.</summary>
    [Theory]
    [InlineData(@"Datei C:\Temp\x.log fehlt")]
    [InlineData(@"Freigabe \\server\share\x.dat fehlt")]
    public void Pfade_bleiben_unveraendert(string eingabe)
    {
        Assert.Equal(eingabe, Maskierer.Maskiere(eingabe));
    }

    /// <summary>Der aktuelle Benutzer wird als ganzes Wort ersetzt, unabhängig von der Schreibweise.</summary>
    [Fact]
    public void Aktueller_Benutzer()
    {
        var maskierer = new DatenMaskierer("jrot");

        Assert.Equal(
            "Pfad von [Benutzer] und [Benutzer], aber jrotation bleibt",
            maskierer.Maskiere("Pfad von jrot und JROT, aber jrotation bleibt"));
    }

    /// <summary>Ohne Angabe wird <see cref="Environment.UserName"/> verwendet.</summary>
    [Fact]
    public void Standard_ist_Environment_UserName()
    {
        var maskierer = new DatenMaskierer();

        Assert.Equal("Hallo [Benutzer]!", maskierer.Maskiere($"Hallo {Environment.UserName}!"));
    }

    /// <summary>Leere Eingabe und Text ohne sensible Daten bleiben unverändert; zweimaliges Maskieren ändert nichts mehr.</summary>
    [Fact]
    public void Randfaelle()
    {
        Assert.Equal("", Maskierer.Maskiere(""));
        Assert.Equal("Alles normal", Maskierer.Maskiere("Alles normal"));

        var einmal = Maskierer.Maskiere("user=abc von 1.2.3.4");
        Assert.Equal(einmal, Maskierer.Maskiere(einmal));
    }
}
