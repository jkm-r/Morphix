using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Morphix.Core.Domaene;

namespace Morphix.App.ViewModels;

/// <summary>Eine Zeile der Aufgaben-Tabelle: die Aufgabe samt Anzeigetexten und Markierung (US11).</summary>
public sealed record AufgabeAnzeige(GeplanteAufgabe Aufgabe)
{
    private static readonly CultureInfo Kultur = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Name der Aufgabe.</summary>
    public string Name => Aufgabe.Name;

    /// <summary>Zeitpunkt des letzten Laufs oder ein Strich.</summary>
    public string LetzterLaufText => Format(Aufgabe.LetzterLauf);

    /// <summary>Zeitpunkt des nächsten Laufs oder ein Strich.</summary>
    public string NaechsterLaufText => Format(Aufgabe.NaechsterLauf);

    /// <summary>GR08: Eine Aufgabe mit letztem Ergebniscode ungleich 0 gilt als fehlgeschlagen und wird rot markiert.</summary>
    public bool IstFehlgeschlagen => Aufgabe.IstFehlgeschlagen;

    /// <summary>Ergebnis des letzten Laufs als deutscher Text.</summary>
    public string ErgebnisText => Aufgabe.LetztesErgebnis switch
    {
        null => Aufgabe.LetzterLauf is null ? "Keine Angaben" : "Läuft oder noch kein Ergebnis",
        0 => "Erfolgreich",
        var code => $"Fehlgeschlagen (Code 0x{code:X})",
    };

    private static string Format(DateTime? zeit) => zeit is { } z ? z.ToString("dd.MM.yyyy HH:mm", Kultur) : "–";
}

/// <summary>
/// ViewModel der Ansicht „Aufgaben“ (US10, US11): zeigt die geplanten Aufgaben des Profils mit letztem Lauf,
/// Ergebnis und nächstem Lauf aus der letzten Analyse. Fehlgeschlagene Aufgaben (GR08) sind rot markiert.
/// </summary>
public sealed partial class AufgabenViewModel : ObservableViewModel
{
    /// <summary>Die Aufgaben der letzten Analyse.</summary>
    public ObservableCollection<AufgabeAnzeige> Aufgaben { get; } = [];

    /// <summary>Hinweis über der Tabelle, z. B. wenn noch keine Analyse vorliegt oder das Profil keine Aufgaben hat.</summary>
    [ObservableProperty]
    public partial string Hinweis { get; set; } = KeineAnalyse;

    /// <summary>Warnung mit der Zahl der fehlgeschlagenen Aufgaben; leer, wenn keine fehlgeschlagen ist.</summary>
    [ObservableProperty]
    public partial string FehlerHinweis { get; set; } = "";

    private const string KeineAnalyse = "Noch keine Analyse durchgeführt.";

    /// <summary>Zeigt die Aufgaben einer Diagnose an; <c>null</c> setzt die Ansicht zurück.</summary>
    public void Zeige(Diagnose? diagnose)
    {
        Aufgaben.Clear();
        FehlerHinweis = "";

        if (diagnose is null)
        {
            Hinweis = KeineAnalyse;
            return;
        }

        foreach (var aufgabe in diagnose.Aufgaben)
        {
            Aufgaben.Add(new AufgabeAnzeige(aufgabe));
        }

        Hinweis = Aufgaben.Count == 0 ? "Das Profil enthält keine geplanten Aufgaben." : "";
        var fehlgeschlagen = Aufgaben.Count(a => a.IstFehlgeschlagen);
        FehlerHinweis = fehlgeschlagen switch
        {
            0 => "",
            1 => "Achtung: 1 Aufgabe ist fehlgeschlagen.",
            _ => $"Achtung: {fehlgeschlagen} Aufgaben sind fehlgeschlagen.",
        };
    }
}
