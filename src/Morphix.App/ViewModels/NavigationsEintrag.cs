namespace Morphix.App.ViewModels;

/// <summary>
/// Ein Eintrag der linken Navigation: Anzeigetext und das ViewModel der zugehörigen Ansicht.
/// </summary>
/// <param name="Titel">Beschriftung in der Navigation.</param>
/// <param name="Ansicht">ViewModel, das beim Auswählen im Inhaltsbereich angezeigt wird.</param>
public sealed record NavigationsEintrag(string Titel, ObservableViewModel Ansicht);
