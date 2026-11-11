using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Morphix.App.Dialoge;
using Morphix.App.ViewModels;
using Morphix.Core.Anwendung;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;
using Morphix.Infrastructure;
using Morphix.Infrastructure.Profile;

namespace Morphix.App;

/// <summary>
/// Einstiegspunkt der Anwendung. Baut beim Start den DI-Container auf und erzeugt das Hauptfenster darüber.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _dienste;

    /// <summary>
    /// Registriert alle Dienste, ViewModels und Fenster im Container.
    /// </summary>
    /// <param name="datenOrdner">Ordner für Profile und Einstellungen; Standard ist <c>%AppData%\Morphix</c>.</param>
    /// <param name="demoOrdner">Ordner mit Demo-Profil und Demo-Logs; Standard ist <c>demo</c> neben der Anwendung.</param>
    public static ServiceProvider BaueContainer(string? datenOrdner = null, string? demoOrdner = null)
    {
        datenOrdner ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Morphix");
        demoOrdner ??= Path.Combine(AppContext.BaseDirectory, "demo");

        var services = new ServiceCollection();

        // Infrastruktur (Adapter hinter den Schnittstellen aus Morphix.Core)
        services.AddSingleton<IProfilRepository>(_ => new JsonProfilRepository(
            Path.Combine(datenOrdner, "Profile"),
            Path.Combine(datenOrdner, "einstellungen.json"),
            Path.Combine(demoOrdner, "demo-profil.json")));
        services.AddSingleton<IDatenquellenFabrik>(_ => new WindowsDatenquellenFabrik(demoOrdner));

        // Anwendung
        services.AddSingleton<ProfilValidator>();
        services.AddSingleton<ProfilService>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DiagnoseService>();

        // Präsentation
        services.AddSingleton<IProfilDialogService, ProfilDialogService>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<LogsUndFehlerViewModel>();
        services.AddSingleton<DiensteViewModel>();
        services.AddSingleton<AufgabenViewModel>();
        services.AddSingleton<BerichtViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }

    /// <inheritdoc />
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _dienste = BaueContainer();
        _dienste.GetRequiredService<MainWindow>().Show();
        await _dienste.GetRequiredService<MainViewModel>().InitialisierenAsync();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _dienste?.Dispose();
        base.OnExit(e);
    }
}
