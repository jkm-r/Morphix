using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Morphix.App.ViewModels;

namespace Morphix.App;

/// <summary>
/// Einstiegspunkt der Anwendung. Baut beim Start den DI-Container auf und erzeugt das Hauptfenster darüber.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _dienste;

    /// <summary>Registriert alle ViewModels und Fenster im Container.</summary>
    public static ServiceProvider BaueContainer()
    {
        var services = new ServiceCollection();

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
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _dienste = BaueContainer();
        _dienste.GetRequiredService<MainWindow>().Show();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _dienste?.Dispose();
        base.OnExit(e);
    }
}
