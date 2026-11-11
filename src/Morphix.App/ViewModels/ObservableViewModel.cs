using CommunityToolkit.Mvvm.ComponentModel;

namespace Morphix.App.ViewModels;

/// <summary>
/// Gemeinsame Basisklasse aller ViewModels. Dient auch als Zieltyp für die DataTemplates,
/// mit denen die Hauptansicht das passende View zum ViewModel auswählt.
/// </summary>
public abstract class ObservableViewModel : ObservableObject
{
}
