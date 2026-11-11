using System.Windows;
using Morphix.App.Views;

namespace Morphix.App.Dialoge;

/// <summary>Fragt den Anwender nach einer Bestätigung (z. B. vor einer Dienstaktion). Erlaubt Tests ohne echtes Fenster.</summary>
public interface IBestaetigungsDialog
{
    /// <summary>Zeigt die Frage mit den Schaltflächen „Ja“ und „Abbrechen“.</summary>
    /// <returns><c>true</c>, wenn der Anwender mit „Ja“ bestätigt hat.</returns>
    bool Bestaetigen(string titel, string frage);
}

/// <summary>WPF-Umsetzung von <see cref="IBestaetigungsDialog"/> (Dialog „Dienstaktion bestätigen“ der Spezifikation).</summary>
public sealed class BestaetigungsDialog : IBestaetigungsDialog
{
    /// <inheritdoc />
    public bool Bestaetigen(string titel, string frage)
    {
        var fenster = new BestaetigungsWindow(titel, frage) { Owner = Application.Current?.MainWindow };
        return fenster.ShowDialog() == true;
    }
}
