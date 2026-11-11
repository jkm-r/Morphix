using Morphix.Core.Domaene;

namespace Morphix.Infrastructure.Dienste;

/// <summary>
/// Dünne Schicht über der Windows-Dienstverwaltung. Sie trennt die Logik des <see cref="WindowsDienstGateway"/>
/// (Statusabbildung, Warten, Fehlermeldungen) vom <c>ServiceController</c>, damit sie ohne echte Dienste
/// mit einem Fake getestet werden kann.
/// </summary>
public interface IDienstZugriff
{
    /// <summary>Öffnet einen Dienst; <c>null</c>, wenn es ihn auf dem System nicht gibt.</summary>
    IDienstHandle? Oeffnen(string dienstname);
}

/// <summary>Ein geöffneter Windows-Dienst.</summary>
public interface IDienstHandle : IDisposable
{
    /// <summary>Anzeigename des Dienstes.</summary>
    string Anzeigename { get; }

    /// <summary>Aktueller Status (nach <see cref="Aktualisieren"/>).</summary>
    DienstStatus Status { get; }

    /// <summary>Starttyp des Dienstes.</summary>
    Starttyp Starttyp { get; }

    /// <summary>Liest den Zustand neu vom System.</summary>
    void Aktualisieren();

    /// <summary>Sendet die Startanforderung.</summary>
    void Starten();

    /// <summary>Sendet die Stoppanforderung.</summary>
    void Stoppen();

    /// <summary>Wartet höchstens <paramref name="zeit"/> auf den Zielstatus; <c>false</c> bei Zeitüberschreitung.</summary>
    bool WarteAuf(DienstStatus ziel, TimeSpan zeit);
}
