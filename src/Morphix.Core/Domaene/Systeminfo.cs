using System.Runtime.InteropServices;

namespace Morphix.Core.Domaene;

/// <summary>Angaben zum Rechner, auf dem der Bericht erstellt wird.</summary>
/// <param name="Rechnername">Name des Rechners (im Bericht maskierbar).</param>
/// <param name="WindowsVersion">Beschreibung des Betriebssystems.</param>
public sealed record Systeminfo(string Rechnername, string WindowsVersion)
{
    /// <summary>Liest die Angaben des laufenden Rechners.</summary>
    public static Systeminfo Aktuell() => new(Environment.MachineName, RuntimeInformation.OSDescription);
}
