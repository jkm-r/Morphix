using System.Windows;
using Morphix.App.ViewModels;
using Morphix.App.Views;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;

namespace Morphix.App.Dialoge;

/// <summary>Öffnet den Dialog „Profil bearbeiten“. Die Schnittstelle erlaubt Tests ohne echtes Fenster.</summary>
public interface IProfilDialogService
{
    /// <summary>Zeigt den Dialog modal.</summary>
    /// <param name="profil">Das zu bearbeitende Profil; <c>null</c> für ein neues Profil.</param>
    /// <returns>Das gespeicherte Profil oder <c>null</c>, wenn der Anwender abgebrochen hat.</returns>
    Systemprofil? Bearbeiten(Systemprofil? profil);
}

/// <summary>WPF-Umsetzung von <see cref="IProfilDialogService"/>.</summary>
public sealed class ProfilDialogService : IProfilDialogService
{
    private readonly ProfilService _service;

    /// <summary>Erzeugt den Dienst.</summary>
    public ProfilDialogService(ProfilService service)
    {
        _service = service;
    }

    /// <inheritdoc />
    public Systemprofil? Bearbeiten(Systemprofil? profil)
    {
        var viewModel = new ProfilBearbeitenViewModel(_service, profil);
        var fenster = new ProfilBearbeitenWindow
        {
            DataContext = viewModel,
            Owner = Application.Current?.MainWindow,
        };
        viewModel.SchliessenAngefordert += ergebnis => fenster.DialogResult = ergebnis;
        return fenster.ShowDialog() == true ? viewModel.Gespeichert : null;
    }
}
