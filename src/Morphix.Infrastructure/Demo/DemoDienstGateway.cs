using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Demo;

/// <summary>
/// Simulierte Dienste (GR04): Zustände liegen nur im Speicher, Aktionen verändern kein echtes System.
/// Den Anfangszustand liefern die Dienste des Demo-Profils.
/// </summary>
public sealed class DemoDienstGateway : IDienstGateway
{
    private readonly Dictionary<string, Dienst> _zustand = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _verzoegerung;
    private readonly object _sperre = new();

    /// <summary>Erzeugt das Gateway.</summary>
    /// <param name="dienste">Dienste des Profils; ein unbekannter Status gilt als gestartet.</param>
    /// <param name="verzoegerung">Simulierte Dauer einer Aktion; ohne Angabe 600 ms.</param>
    public DemoDienstGateway(IEnumerable<Dienst> dienste, TimeSpan? verzoegerung = null)
    {
        _verzoegerung = verzoegerung ?? TimeSpan.FromMilliseconds(600);
        foreach (var d in dienste)
        {
            _zustand[d.Dienstname] = d with
            {
                Status = d.Status == DienstStatus.Unbekannt ? DienstStatus.Gestartet : d.Status,
                Starttyp = d.Starttyp == Starttyp.Unbekannt ? Starttyp.Automatisch : d.Starttyp,
            };
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Dienst>> AbfragenAsync(IEnumerable<Dienst> dienste, CancellationToken abbruch = default)
    {
        lock (_sperre)
        {
            IReadOnlyList<Dienst> ergebnis =
            [
                .. dienste.Select(d => _zustand.TryGetValue(d.Dienstname, out var z)
                    ? d with { Status = z.Status, Starttyp = z.Starttyp, Anzeigename = z.Anzeigename }
                    : d with { Status = DienstStatus.NichtGefunden }),
            ];
            return Task.FromResult(ergebnis);
        }
    }

    /// <inheritdoc />
    public async Task<Dienst> AusfuehrenAsync(Dienst dienst, DienstAktionsart aktion, CancellationToken abbruch = default)
    {
        lock (_sperre)
        {
            if (!_zustand.ContainsKey(dienst.Dienstname))
            {
                throw new InvalidOperationException($"Der Dienst '{dienst.Dienstname}' ist nicht vorhanden.");
            }
        }

        await Task.Delay(_verzoegerung, abbruch);

        lock (_sperre)
        {
            var neu = _zustand[dienst.Dienstname] with
            {
                Status = aktion == DienstAktionsart.Stoppen ? DienstStatus.Gestoppt : DienstStatus.Gestartet,
            };
            _zustand[dienst.Dienstname] = neu;
            return neu;
        }
    }
}
