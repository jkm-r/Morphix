using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Morphix.Core.Domaene;
using OxyPlot;

namespace Morphix.App.ViewModels;

/// <summary>
/// ViewModel des Dashboards: drei Kennzahlen und die häufigsten Fehlergruppen der letzten Analyse.
/// </summary>
public sealed partial class DashboardViewModel : ObservableViewModel
{
    private const int AnzahlGruppen = 5;
    private const string Leer = "–";

    /// <summary>Wird ausgelöst, wenn der Anwender „Bericht erstellen“ wählt; das Hauptfenster wechselt dann zur Ansicht „Bericht“.</summary>
    public event Action? BerichtAngefordert;

    /// <summary>Die häufigsten Fehlergruppen (höchstens fünf).</summary>
    public ObservableCollection<Fehlergruppe> HaeufigsteGruppen { get; } = [];

    /// <summary>Anzahl der Fehler (Schweregrad Fehler oder Kritisch) im Zeitraum.</summary>
    [ObservableProperty]
    public partial string FehlerAnzahl { get; set; } = Leer;

    /// <summary>Beschriftung zur Fehleranzahl.</summary>
    public string FehlerText => "Fehler im Zeitraum";

    /// <summary>Anzahl gestoppter kritischer Dienste.</summary>
    [ObservableProperty]
    public partial string GestoppteKritischeDienste { get; set; } = Leer;

    /// <summary>Beschriftung zu den gestoppten kritischen Diensten (Einzahl/Mehrzahl).</summary>
    [ObservableProperty]
    public partial string KritischeDiensteText { get; set; } = "Kritische Dienste gestoppt";

    /// <summary>Anzahl fehlgeschlagener Aufgaben (GR08).</summary>
    [ObservableProperty]
    public partial string FehlgeschlageneAufgaben { get; set; } = Leer;

    /// <summary>Beschriftung zu den fehlgeschlagenen Aufgaben (Einzahl/Mehrzahl).</summary>
    [ObservableProperty]
    public partial string AufgabenText { get; set; } = "Aufgaben fehlgeschlagen";

    /// <summary>Hinweis unter der Tabelle, z. B. zu übersprungenen Dateien oder fehlenden Fehlern.</summary>
    [ObservableProperty]
    public partial string Hinweis { get; set; } = "Noch keine Analyse durchgeführt.";

    /// <summary>Diagramm der Fehler pro Stunde bzw. Tag (US12); <c>null</c>, solange keine Diagnose vorliegt.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HatDiagramm))]
    public partial PlotModel? Diagramm { get; set; }

    /// <summary>Ob ein Diagramm angezeigt wird.</summary>
    public bool HatDiagramm => Diagramm is not null;

    /// <summary>Ob eine Diagnose vorliegt.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BerichtErstellenCommand))]
    public partial bool HatDiagnose { get; set; }

    /// <summary>Zeigt die Kennzahlen einer Diagnose an; <c>null</c> setzt die Ansicht zurück.</summary>
    public void Zeige(Diagnose? diagnose)
    {
        HaeufigsteGruppen.Clear();
        HatDiagnose = diagnose is not null;
        Diagramm = diagnose is null
            ? null
            : VerlaufDiagramm.Erzeuge(FehlerVerlauf.Berechne(diagnose.Logeintraege, diagnose.Von, diagnose.Bis));

        if (diagnose is null)
        {
            FehlerAnzahl = GestoppteKritischeDienste = FehlgeschlageneAufgaben = Leer;
            KritischeDiensteText = "Kritische Dienste gestoppt";
            AufgabenText = "Aufgaben fehlgeschlagen";
            Hinweis = "Noch keine Analyse durchgeführt.";
            return;
        }

        var fehler = diagnose.Logeintraege.Count(e => e.Schweregrad is Schweregrad.Fehler or Schweregrad.Kritisch);
        var gestoppt = diagnose.Dienste.Count(d => d.IstKritisch && d.Status == DienstStatus.Gestoppt);
        var fehlgeschlagen = diagnose.Aufgaben.Count(a => a.IstFehlgeschlagen);

        FehlerAnzahl = fehler.ToString();
        GestoppteKritischeDienste = gestoppt.ToString();
        KritischeDiensteText = gestoppt == 1 ? "Kritischer Dienst gestoppt" : "Kritische Dienste gestoppt";
        FehlgeschlageneAufgaben = fehlgeschlagen.ToString();
        AufgabenText = fehlgeschlagen == 1 ? "Aufgabe fehlgeschlagen" : "Aufgaben fehlgeschlagen";

        foreach (var g in diagnose.Fehlergruppen.Take(AnzahlGruppen))
        {
            HaeufigsteGruppen.Add(g);
        }

        Hinweis = BaueHinweis(diagnose);
    }

    [RelayCommand(CanExecute = nameof(HatDiagnose))]
    private void BerichtErstellen() => BerichtAngefordert?.Invoke();

    private static string BaueHinweis(Diagnose diagnose)
    {
        var teile = new List<string>();
        if (diagnose.Fehlergruppen.Count == 0)
        {
            teile.Add("Im Zeitraum wurden keine Fehler gefunden.");
        }

        if (diagnose.UebersprungeneDateien.Count > 0)
        {
            var liste = string.Join("; ", diagnose.UebersprungeneDateien.Select(u => $"{Path.GetFileName(u.Pfad)} ({u.Grund})"));
            teile.Add($"{diagnose.UebersprungeneDateien.Count} Datei(en) übersprungen: {liste}");
        }

        return string.Join(Environment.NewLine, teile);
    }
}
