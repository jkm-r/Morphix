using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace Morphix.App.Dialoge;

/// <summary>Lässt den Anwender Ort und Namen der ZIP-Datei wählen (Dialog „Export“). Erlaubt Tests ohne echtes Fenster.</summary>
public interface IExportDialog
{
    /// <summary>Zeigt den Speichern-Dialog mit vorgeschlagenem Dateinamen.</summary>
    /// <param name="vorgeschlagenerName">Vorschlag für den Dateinamen, z. B. <c>Morphix_Profil_20261111_1430.zip</c>.</param>
    /// <returns>Der gewählte Pfad oder <c>null</c>, wenn der Anwender abgebrochen hat.</returns>
    string? ZielWaehlen(string vorgeschlagenerName);
}

/// <summary>WPF-Umsetzung von <see cref="IExportDialog"/> mit dem Windows-Speichern-Dialog.</summary>
public sealed class ExportDialog : IExportDialog
{
    /// <inheritdoc />
    public string? ZielWaehlen(string vorgeschlagenerName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Support-Bericht exportieren",
            Filter = "ZIP-Archiv (*.zip)|*.zip",
            DefaultExt = ".zip",
            AddExtension = true,
            FileName = vorgeschlagenerName,
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            OverwritePrompt = true,
        };

        return dialog.ShowDialog(Application.Current?.MainWindow) == true ? Path.GetFullPath(dialog.FileName) : null;
    }
}
