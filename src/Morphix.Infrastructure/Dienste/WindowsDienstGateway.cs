using System.ComponentModel;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Dienste;

/// <summary>
/// Fragt Windows-Dienste ab und steuert sie (Adapter hinter <see cref="IDienstGateway"/>). Nach Start, Stopp
/// oder Neustart wird bis zu 30 Sekunden auf den Zielstatus gewartet (P5). Ein Dienst, den es nicht gibt,
/// kommt mit <see cref="DienstStatus.NichtGefunden"/> zurück.
/// </summary>
public sealed class WindowsDienstGateway : IDienstGateway
{
    private const int FehlerZugriffVerweigert = 5; // ERROR_ACCESS_DENIED

    /// <summary>Wartezeit auf den Zielstatus laut Spezifikation.</summary>
    public static readonly TimeSpan StandardTimeout = TimeSpan.FromSeconds(30);

    private readonly IDienstZugriff _zugriff;
    private readonly TimeSpan _timeout;

    /// <summary>Erzeugt das Gateway.</summary>
    /// <param name="zugriff">Zugriff auf die Dienstverwaltung; ohne Angabe der echte <see cref="ServiceControllerZugriff"/>.</param>
    /// <param name="timeout">Wartezeit auf den Zielstatus; ohne Angabe 30 Sekunden. Tests übergeben eine kurze Zeit.</param>
    public WindowsDienstGateway(IDienstZugriff? zugriff = null, TimeSpan? timeout = null)
    {
        _zugriff = zugriff ?? new ServiceControllerZugriff();
        _timeout = timeout ?? StandardTimeout;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Dienst>> AbfragenAsync(IEnumerable<Dienst> dienste, CancellationToken abbruch = default)
    {
        var liste = dienste.ToList();
        return Task.Run<IReadOnlyList<Dienst>>(() => [.. liste.Select(d => Abfragen(d, abbruch))], abbruch);
    }

    /// <inheritdoc />
    public Task<Dienst> AusfuehrenAsync(Dienst dienst, DienstAktionsart aktion, CancellationToken abbruch = default) =>
        Task.Run(() => Ausfuehren(dienst, aktion, abbruch), abbruch);

    private Dienst Abfragen(Dienst dienst, CancellationToken abbruch)
    {
        abbruch.ThrowIfCancellationRequested();
        try
        {
            using var handle = _zugriff.Oeffnen(dienst.Dienstname);
            return handle is null
                ? dienst with { Status = DienstStatus.NichtGefunden }
                : dienst with { Status = handle.Status, Starttyp = handle.Starttyp, Anzeigename = handle.Anzeigename };
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            // Der Zustand ist nicht lesbar (z. B. fehlende Rechte am Dienst): als unbekannt anzeigen statt abbrechen.
            return dienst with { Status = DienstStatus.Unbekannt };
        }
    }

    private Dienst Ausfuehren(Dienst dienst, DienstAktionsart aktion, CancellationToken abbruch)
    {
        abbruch.ThrowIfCancellationRequested();
        try
        {
            using var handle = _zugriff.Oeffnen(dienst.Dienstname)
                ?? throw new DienstAktionFehlgeschlagenException(
                    $"Der Dienst „{dienst.Dienstname}“ ist auf dem System nicht vorhanden.");

            switch (aktion)
            {
                case DienstAktionsart.Starten:
                    Starte(handle, dienst);
                    break;
                case DienstAktionsart.Stoppen:
                    Stoppe(handle, dienst);
                    break;
                default:
                    Stoppe(handle, dienst);
                    Starte(handle, dienst);
                    break;
            }

            handle.Aktualisieren();
            return dienst with { Status = handle.Status, Starttyp = handle.Starttyp, Anzeigename = handle.Anzeigename };
        }
        catch (InvalidOperationException ex)
        {
            throw new DienstAktionFehlgeschlagenException(Beschreibe(dienst, ex), ex);
        }
    }

    private void Starte(IDienstHandle handle, Dienst dienst)
    {
        handle.Aktualisieren();
        if (handle.Status is DienstStatus.Gestartet)
        {
            return;
        }

        if (handle.Status is not DienstStatus.Startet)
        {
            handle.Starten();
        }

        WarteOderMelde(handle, dienst, DienstStatus.Gestartet, "gestartet");
    }

    private void Stoppe(IDienstHandle handle, Dienst dienst)
    {
        handle.Aktualisieren();
        if (handle.Status is DienstStatus.Gestoppt)
        {
            return;
        }

        if (handle.Status is not DienstStatus.Stoppt)
        {
            handle.Stoppen();
        }

        WarteOderMelde(handle, dienst, DienstStatus.Gestoppt, "gestoppt");
    }

    private void WarteOderMelde(IDienstHandle handle, Dienst dienst, DienstStatus ziel, string verb)
    {
        if (!handle.WarteAuf(ziel, _timeout))
        {
            throw new DienstAktionFehlgeschlagenException(
                $"Der Dienst „{dienst.Dienstname}“ wurde nicht innerhalb von {(int)_timeout.TotalSeconds} Sekunden {verb}.");
        }
    }

    private static string Beschreibe(Dienst dienst, InvalidOperationException ex)
    {
        if (ex.InnerException is Win32Exception { NativeErrorCode: FehlerZugriffVerweigert })
        {
            return $"Zugriff auf den Dienst „{dienst.Dienstname}“ verweigert. Dafür sind Administratorrechte erforderlich.";
        }

        var grund = ex.InnerException?.Message ?? ex.Message;
        return $"Die Aktion auf den Dienst „{dienst.Dienstname}“ ist fehlgeschlagen: {grund}";
    }
}
