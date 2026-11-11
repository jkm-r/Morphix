using System.Windows;

namespace Morphix.App.Views;

/// <summary>
/// Bestätigungsdialog mit „Ja“ und „Abbrechen“. „Abbrechen“ ist die Standardschaltfläche, damit ein
/// versehentliches Drücken der Eingabetaste keinen Dienst steuert. Reines View-Verhalten ohne Fachlogik.
/// </summary>
public partial class BestaetigungsWindow : Window
{
    /// <summary>Erzeugt den Dialog.</summary>
    public BestaetigungsWindow(string titel, string frage)
    {
        InitializeComponent();
        Title = titel;
        Frage.Text = frage;
    }

    private void Ja_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
