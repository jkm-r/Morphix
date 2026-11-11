using Morphix.Core.Domaene;

namespace Morphix.Core.Schnittstellen;

/// <summary>Zugriff auf Windows-Dienste (Port). Die Rechteprüfung (GR03) liegt im <c>DienstService</c>, nicht hier.</summary>
public interface IDienstGateway
{
    /// <summary>
    /// Fragt Status und Starttyp der Dienste ab. Ein Dienst, den es nicht gibt, kommt mit
    /// <see cref="DienstStatus.NichtGefunden"/> zurück. Die Reihenfolge der Eingabe bleibt erhalten.
    /// </summary>
    Task<IReadOnlyList<Dienst>> AbfragenAsync(IEnumerable<Dienst> dienste, CancellationToken abbruch = default);

    /// <summary>Führt eine Aktion aus und wartet auf den Zielstatus (höchstens 30 Sekunden).</summary>
    /// <returns>Der Dienst im Zustand nach der Aktion.</returns>
    Task<Dienst> AusfuehrenAsync(Dienst dienst, DienstAktionsart aktion, CancellationToken abbruch = default);
}
