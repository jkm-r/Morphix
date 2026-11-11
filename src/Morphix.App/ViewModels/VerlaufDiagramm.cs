using System.Globalization;
using Morphix.Core.Domaene;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace Morphix.App.ViewModels;

/// <summary>
/// Baut aus einem <see cref="FehlerVerlauf"/> das OxyPlot-Diagramm für das Dashboard (US12): senkrechte, gestapelte
/// Säulen für kritische Einträge (unten) und Fehler (oben) je Stunde bzw. Tag. Das Modell ist unabhängig von der
/// Oberfläche und deshalb testbar.
/// </summary>
/// <remarks>
/// OxyPlot 2.x kennt keine <c>ColumnSeries</c> mehr; <c>BarSeries</c> zeichnet nur waagerechte Balken. Die Säulen sind
/// deshalb <see cref="RectangleBarSeries"/> auf einer Datumsachse, jede Säule füllt 80 % ihres Zeitabschnitts.
/// </remarks>
public static class VerlaufDiagramm
{
    private static readonly CultureInfo Kultur = CultureInfo.GetCultureInfo("de-DE");

    /// <summary>Anteil des Zeitabschnitts, den eine Säule ausfüllt (der Rest ist Abstand).</summary>
    private const double Fuellung = 0.8;

    /// <summary>Höchstzahl beschrifteter Zeitabschnitte auf der Achse, damit die Beschriftung lesbar bleibt.</summary>
    private const int MaxBeschriftungen = 12;

    /// <summary>Farbe der Fehler (wie die Kennzahl-Kachel der Aufgaben im Dashboard).</summary>
    public static readonly OxyColor FehlerFarbe = OxyColor.FromRgb(0xF2, 0xB8, 0x4B);

    /// <summary>Farbe der kritischen Einträge (wie die Hervorhebung kritischer Dienste).</summary>
    public static readonly OxyColor KritischFarbe = OxyColor.FromRgb(0xC6, 0x28, 0x28);

    /// <summary>Erzeugt das Diagramm.</summary>
    public static PlotModel Erzeuge(FehlerVerlauf verlauf)
    {
        ArgumentNullException.ThrowIfNull(verlauf);

        var proStunde = verlauf.Aufloesung == Zeitaufloesung.Stunde;
        var einheit = proStunde ? TimeSpan.FromHours(1) : TimeSpan.FromDays(1);
        var anzahl = verlauf.Punkte.Count;
        var beschriftungsSchritt = Math.Max(1, (int)Math.Ceiling(anzahl / (double)MaxBeschriftungen));

        var modell = new PlotModel
        {
            Title = proStunde ? "Fehler pro Stunde" : "Fehler pro Tag",
            TitleFontSize = 13,
            TitleFontWeight = OxyPlot.FontWeights.Bold,
            Culture = Kultur,
        };
        modell.Legends.Add(new Legend { LegendPosition = LegendPosition.RightMiddle, LegendPlacement = LegendPlacement.Outside });

        // Die Datumsachse reicht vom Beginn des ersten bis zum Ende des letzten Abschnitts.
        var beginn = anzahl > 0 ? verlauf.Punkte[0].Beginn : DateTime.Today;
        var datumsachse = new DateTimeAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = DateTimeAxis.ToDouble(beginn),
            Maximum = DateTimeAxis.ToDouble(beginn + anzahl * einheit),
            IntervalType = proStunde ? DateTimeIntervalType.Hours : DateTimeIntervalType.Days,
            MajorStep = beschriftungsSchritt * einheit.TotalDays,
            MinorStep = einheit.TotalDays,
            StringFormat = proStunde ? "dd.MM. HH:mm" : "dd.MM.",
            Angle = anzahl > MaxBeschriftungen / 2 ? -45 : 0,
            IsZoomEnabled = false,
            IsPanEnabled = false,
        };
        modell.Axes.Add(datumsachse);

        modell.Axes.Add(new LinearAxis
        {
            Position = AxisPosition.Left,
            Title = "Anzahl",
            Minimum = 0,
            Maximum = Math.Max(verlauf.Maximum, 1) * 1.15,
            MinimumMajorStep = 1,
            StringFormat = "0",
            IsZoomEnabled = false,
            IsPanEnabled = false,
        });

        var kritisch = new RectangleBarSeries { Title = "Kritisch", FillColor = KritischFarbe, StrokeThickness = 0, LabelFormatString = null, TrackerFormatString = "{7}" };
        var fehler = new RectangleBarSeries { Title = "Fehler", FillColor = FehlerFarbe, StrokeThickness = 0, LabelFormatString = null, TrackerFormatString = "{7}" };
        foreach (var punkt in verlauf.Punkte)
        {
            var links = DateTimeAxis.ToDouble(punkt.Beginn + einheit * ((1 - Fuellung) / 2));
            var rechts = DateTimeAxis.ToDouble(punkt.Beginn + einheit * (1 - (1 - Fuellung) / 2));

            // Gestapelt: kritische Einträge unten, Fehler darüber. Leere Säulen werden nicht gezeichnet.
            if (punkt.Kritisch > 0)
            {
                kritisch.Items.Add(new RectangleBarItem(links, 0, rechts, punkt.Kritisch) { Title = $"Kritisch: {punkt.Kritisch}" });
            }

            if (punkt.Fehler > 0)
            {
                fehler.Items.Add(new RectangleBarItem(links, punkt.Kritisch, rechts, punkt.Gesamt) { Title = $"Fehler: {punkt.Fehler}" });
            }
        }

        modell.Series.Add(kritisch);
        modell.Series.Add(fehler);
        return modell;
    }
}
