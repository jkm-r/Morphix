using System.Windows;
using Morphix.App.ViewModels;

namespace Morphix.App;

/// <summary>Hauptfenster. Enthält keine Logik; alles läuft über das <see cref="MainViewModel"/>.</summary>
public partial class MainWindow : Window
{
    /// <summary>Erzeugt das Fenster und setzt das ViewModel als DataContext.</summary>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
