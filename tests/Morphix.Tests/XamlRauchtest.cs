using System.Windows;
using System.Windows.Controls;
using Morphix.App.ViewModels;
using Morphix.App.Views;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Morphix.Core.Schnittstellen;

namespace Morphix.Tests;

/// <summary>
/// Rauchtest für die XAML-Oberfläche: Alle Ansichten und Fenster müssen sich mit den Anwendungsressourcen
/// laden und layouten lassen. Fehlende Ressourcen oder falsche Bindungspfade-Syntax fielen sonst erst beim
/// Anklicken der Navigation auf.
/// </summary>
public class XamlRauchtest
{
    private static void InStaThread(Action aktion)
    {
        Exception? fehler = null;
        var thread = new Thread(() =>
        {
            try
            {
                aktion();
            }
            catch (Exception ex)
            {
                fehler = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (fehler is not null)
        {
            throw new InvalidOperationException("XAML-Rauchtest fehlgeschlagen: " + fehler, fehler);
        }
    }

    private static void Layoute(FrameworkElement element)
    {
        element.Measure(new Size(1000, 700));
        element.Arrange(new Rect(0, 0, 1000, 700));
        element.UpdateLayout();
    }

    /// <summary>Hauptfenster, alle Ansichten und der Profildialog laden fehlerfrei.</summary>
    [Fact]
    public void Alle_Ansichten_laden()
    {
        InStaThread(() =>
        {
            var app = Application.Current ?? new Morphix.App.App();
            if (app.Resources.Count == 0)
            {
                ((Morphix.App.App)app).InitializeComponent();
            }

            using var container = Morphix.App.App.BaueContainer(
                System.IO.Path.Combine(System.IO.Path.GetTempPath(), "morphix-xaml-" + Guid.NewGuid().ToString("N")));
            var main = container.GetRequiredService<MainViewModel>();

            // Hauptfenster mit allen fünf Ansichten (die DataTemplates erzeugen die Views).
            var fenster = container.GetRequiredService<Morphix.App.MainWindow>();
            foreach (var eintrag in main.Navigation)
            {
                main.AusgewaehlterEintrag = eintrag;
                Layoute(fenster);
            }

            // Ansichten mit gefüllten Daten, damit auch die Spaltenbindungen ausgewertet werden.
            var diagnose = new Diagnose
            {
                Profil = new Systemprofil { Name = "P" },
                Von = DateTime.Today.AddDays(-1),
                Bis = DateTime.Today,
                Logeintraege = [new Logeintrag(DateTime.Today, Schweregrad.Fehler, "a.log", "Timeout 1")],
                Fehlergruppen = FehlerGruppierer.Gruppiere([new Logeintrag(DateTime.Today, Schweregrad.Fehler, "a.log", "Timeout 1")]),
                IstAbgeschlossen = true,
            };
            container.GetRequiredService<DashboardViewModel>().Zeige(diagnose);
            container.GetRequiredService<LogsUndFehlerViewModel>().Zeige(diagnose);
            main.AusgewaehlterEintrag = main.Navigation[0];
            Layoute(fenster);
            main.AusgewaehlterEintrag = main.Navigation[1];
            Layoute(fenster);

            // Dienste-Ansicht mit simulierten (Demo-)Diensten; es wird nie ein echter Dienst angefasst.
            var demo = new Systemprofil
            {
                Name = "Demo",
                IstDemo = true,
                Logquellen = [new Logquelle { Pfad = "logs" }],
                Dienste =
                [
                    new Dienst { Dienstname = "A", IstKritisch = true, Status = DienstStatus.Gestoppt },
                    new Dienst { Dienstname = "B", Status = DienstStatus.Gestartet },
                    new Dienst { Dienstname = "C", Status = DienstStatus.NichtGefunden },
                ],
            };
            var diensteVm = container.GetRequiredService<DiensteViewModel>();
            diensteVm.SetzeProfil(demo);
            diensteVm.AktualisierenAsync().GetAwaiter().GetResult();
            Assert.Equal(3, diensteVm.Dienste.Count);
            main.AusgewaehlterEintrag = main.Navigation[2];
            Layoute(fenster);
            diensteVm.IstAktiv = false;

            // Bestätigungsdialog (Ja / Abbrechen).
            Layoute(new BestaetigungsWindow("Dienstaktion bestätigen", "Dienst A wirklich stoppen?"));

            // Profildialog (neu und mit vorhandenem Profil).
            var service = container.GetRequiredService<ProfilService>();
            var dialog = new ProfilBearbeitenWindow { DataContext = new ProfilBearbeitenViewModel(service, null) };
            Layoute(dialog);
            var profil = new Systemprofil
            {
                Name = "Vorhanden",
                Logquellen = [new Logquelle { Pfad = "x" }],
                Dienste = [new Dienst { Dienstname = "d" }],
                Aufgaben = [new GeplanteAufgabe { Name = "a" }],
            };
            var dialog2 = new ProfilBearbeitenWindow { DataContext = new ProfilBearbeitenViewModel(service, profil) };
            Layoute(dialog2);
        });
    }
}
