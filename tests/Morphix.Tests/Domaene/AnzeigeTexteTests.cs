using Morphix.Core.Domaene;

namespace Morphix.Tests.Domaene;

/// <summary>Tests der deutschen Anzeigetexte für Dienststatus und Starttyp (Oberfläche und Bericht).</summary>
public class AnzeigeTexteTests
{
    /// <summary>Jeder Dienststatus hat einen deutschen Text.</summary>
    [Theory]
    [InlineData(DienstStatus.Unbekannt, "Unbekannt")]
    [InlineData(DienstStatus.Gestartet, "Gestartet")]
    [InlineData(DienstStatus.Gestoppt, "Gestoppt")]
    [InlineData(DienstStatus.Startet, "Startet …")]
    [InlineData(DienstStatus.Stoppt, "Stoppt …")]
    [InlineData(DienstStatus.Angehalten, "Angehalten")]
    [InlineData(DienstStatus.NichtGefunden, "Nicht gefunden")]
    public void Status_Text(DienstStatus status, string erwartet) => Assert.Equal(erwartet, status.Text());

    /// <summary>Jeder Starttyp hat einen deutschen Text; „unbekannt“ bleibt leer.</summary>
    [Theory]
    [InlineData(Starttyp.Unbekannt, "")]
    [InlineData(Starttyp.Automatisch, "Automatisch")]
    [InlineData(Starttyp.Manuell, "Manuell")]
    [InlineData(Starttyp.Deaktiviert, "Deaktiviert")]
    public void Starttyp_Text(Starttyp starttyp, string erwartet) => Assert.Equal(erwartet, starttyp.Text());
}
