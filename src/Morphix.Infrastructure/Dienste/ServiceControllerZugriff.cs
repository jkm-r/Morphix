using System.ComponentModel;
using System.ServiceProcess;
using Morphix.Core.Domaene;

namespace Morphix.Infrastructure.Dienste;

/// <summary>
/// Umsetzung von <see cref="IDienstZugriff"/> mit dem <see cref="ServiceController"/>. Diese Klasse spricht als
/// einzige mit dem echten Service Control Manager und wird in automatischen Tests nicht verwendet.
/// </summary>
public sealed class ServiceControllerZugriff : IDienstZugriff
{
    private const int FehlerDienstNichtVorhanden = 1060; // ERROR_SERVICE_DOES_NOT_EXIST

    /// <inheritdoc />
    public IDienstHandle? Oeffnen(string dienstname)
    {
        var controller = new ServiceController(dienstname);
        try
        {
            // Der Konstruktor prüft nichts; erst der Zugriff auf Eigenschaften fragt den Dienst ab.
            _ = controller.Status;
            return new Handle(controller);
        }
        catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception { NativeErrorCode: FehlerDienstNichtVorhanden })
        {
            controller.Dispose();
            return null;
        }
        catch
        {
            controller.Dispose();
            throw;
        }
    }

    private sealed class Handle(ServiceController controller) : IDienstHandle
    {
        public string Anzeigename => controller.DisplayName;

        public DienstStatus Status => Abbilden(controller.Status);

        public Starttyp Starttyp => controller.StartType switch
        {
            ServiceStartMode.Automatic or ServiceStartMode.Boot or ServiceStartMode.System => Starttyp.Automatisch,
            ServiceStartMode.Manual => Starttyp.Manuell,
            ServiceStartMode.Disabled => Starttyp.Deaktiviert,
            _ => Starttyp.Unbekannt,
        };

        public void Aktualisieren() => controller.Refresh();

        public void Starten() => controller.Start();

        public void Stoppen() => controller.Stop();

        public bool WarteAuf(DienstStatus ziel, TimeSpan zeit)
        {
            var zielStatus = ziel == DienstStatus.Gestartet ? ServiceControllerStatus.Running : ServiceControllerStatus.Stopped;
            try
            {
                controller.WaitForStatus(zielStatus, zeit);
                return true;
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                return false;
            }
        }

        public void Dispose() => controller.Dispose();

        private static DienstStatus Abbilden(ServiceControllerStatus status) => status switch
        {
            ServiceControllerStatus.Running => DienstStatus.Gestartet,
            ServiceControllerStatus.Stopped => DienstStatus.Gestoppt,
            ServiceControllerStatus.StartPending or ServiceControllerStatus.ContinuePending => DienstStatus.Startet,
            ServiceControllerStatus.StopPending or ServiceControllerStatus.PausePending => DienstStatus.Stoppt,
            ServiceControllerStatus.Paused => DienstStatus.Angehalten,
            _ => DienstStatus.Unbekannt,
        };
    }
}
