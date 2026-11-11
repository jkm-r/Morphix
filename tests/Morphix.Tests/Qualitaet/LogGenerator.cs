using System.Globalization;
using System.Text;

namespace Morphix.Tests.Qualitaet;

/// <summary>
/// Erzeugt große, realistisch aufgebaute Logdateien für den Performance-Test (fester Seed, reproduzierbar).
/// Etwa 94 % Info, 4 % Warnung, 2 % Fehler; jeder 25. Fehler hat einen mehrzeiligen Stacktrace (GR06).
/// </summary>
internal static class LogGenerator
{
    private static readonly string[] Infos =
    [
        "Anmeldung von Benutzer {0} an Sitzung {1} erfolgreich",
        "Anfrage {0} in {1} ms verarbeitet",
        "Konfiguration neu geladen, Version {0}",
        "Verbindungspool: {0} von {1} Verbindungen belegt",
    ];

    private static readonly string[] Warnungen =
    [
        "Plattenplatz auf Laufwerk D: unter {0} Prozent",
        "Abfrage {0} dauerte ungewöhnlich lange ({1} ms)",
    ];

    private static readonly string[] Fehler =
    [
        "Timeout nach {0} s bei Gegenstelle {1}",
        "Datenbankverbindung zu 10.0.{0}.{1} verloren",
        "Datensicherung beendet mit Code {0}",
        "Datei C:\\Daten\\Sicherung\\job{0}.dat konnte nicht geschrieben werden",
    ];

    /// <summary>Schreibt eine Datei mit mindestens <paramref name="zeilen"/> Zeilen und gibt die tatsächliche Zeilenzahl zurück.</summary>
    /// <param name="pfad">Zieldatei.</param>
    /// <param name="zeilen">Gewünschte Zeilenzahl (Stacktrace-Zeilen zählen mit).</param>
    /// <param name="start">Zeitstempel der ersten Zeile.</param>
    /// <param name="dauer">Zeitspanne, über die die Zeilen gleichmäßig verteilt werden.</param>
    public static int Schreibe(string pfad, int zeilen, DateTime start, TimeSpan dauer)
    {
        var zufall = new Random(42);
        var schritt = dauer.TotalMilliseconds / zeilen;
        var geschrieben = 0;
        var fehlerNr = 0;

        using var schreiber = new StreamWriter(pfad, append: false, new UTF8Encoding(false), bufferSize: 1 << 20);
        for (var i = 0; geschrieben < zeilen; i++)
        {
            var zeit = start.AddMilliseconds(i * schritt).ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
            var wurf = zufall.Next(100);
            if (wurf < 2)
            {
                schreiber.WriteLine($"{zeit} ERROR {Formatiere(Fehler, zufall)}");
                geschrieben++;
                if (++fehlerNr % 25 == 0)
                {
                    schreiber.WriteLine("   bei Muster.Server.Anfrage.Verarbeite(Int32 id) in Anfrage.cs:Zeile 112");
                    schreiber.WriteLine("   bei Muster.Server.Dienst.Lauf() in Dienst.cs:Zeile 48");
                    geschrieben += 2;
                }
            }
            else if (wurf < 6)
            {
                schreiber.WriteLine($"{zeit} WARN {Formatiere(Warnungen, zufall)}");
                geschrieben++;
            }
            else
            {
                schreiber.WriteLine($"{zeit} INFO {Formatiere(Infos, zufall)}");
                geschrieben++;
            }
        }

        return geschrieben;
    }

    private static string Formatiere(string[] vorlagen, Random zufall) =>
        string.Format(CultureInfo.InvariantCulture, vorlagen[zufall.Next(vorlagen.Length)], zufall.Next(1, 250), zufall.Next(1, 250));
}
