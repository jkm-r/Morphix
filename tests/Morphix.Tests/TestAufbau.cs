using Morphix.App.Dialoge;
using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Schnittstellen;
using NSubstitute;

namespace Morphix.Tests;

/// <summary>Gemeinsame Hilfen zum Aufbau von Objekten für Tests. Es werden ausschließlich Fakes verwendet, nie echte Dienste.</summary>
internal static class TestAufbau
{
    /// <summary>Datenquellen aus lauter Fakes; die Rechteprüfung liefert <c>false</c>.</summary>
    public static Datenquellen FakeDatenquellen() => new(
        Substitute.For<ILogLeser>(),
        Substitute.For<IDienstGateway>(),
        Substitute.For<IAufgabenGateway>(),
        Substitute.For<IEreignisLeser>(),
        Substitute.For<IRechtePruefer>());

    /// <summary>Eine Fabrik, die immer dieselben Fake-Datenquellen liefert.</summary>
    public static IDatenquellenFabrik FakeFabrik(Datenquellen? quellen = null)
    {
        var fabrik = Substitute.For<IDatenquellenFabrik>();
        fabrik.Erzeugen(Arg.Any<Morphix.Core.Domaene.Systemprofil>()).Returns(quellen ?? FakeDatenquellen());
        return fabrik;
    }

    /// <summary>Ein <see cref="DiensteViewModel"/> mit Fake-Datenquellen und Fake-Bestätigungsdialog.</summary>
    public static DiensteViewModel Dienste() =>
        new(new DienstService(FakeFabrik()), Substitute.For<IBestaetigungsDialog>());
}
