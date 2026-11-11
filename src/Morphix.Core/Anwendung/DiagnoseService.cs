using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Core.Anwendung;

/// <summary>
/// Steuert die Prozesse P2, P3 und P6: Zeitraum prüfen (GR01), Logs, Dienste und Aufgaben parallel erfassen,
/// Fehler gruppieren und eine abgeschlossene <see cref="Diagnose"/> erzeugen. Der Service kennt den
/// Unterschied zwischen echten und simulierten Datenquellen nicht; die Fabrik entscheidet.
/// </summary>
public sealed class DiagnoseService
{
    // Anteil des Gesamtfortschritts, der auf das Einlesen der Logquellen entfällt.
    private const int LogAnteil = 80;

    private readonly IDatenquellenFabrik _fabrik;
    private readonly TimeProvider _zeit;

    /// <summary>Erzeugt den Service.</summary>
    /// <param name="fabrik">Liefert die Datenquellen je Profil.</param>
    /// <param name="zeit">Uhr; Tests übergeben eine feste Zeit. Ohne Angabe gilt die Systemuhr.</param>
    public DiagnoseService(IDatenquellenFabrik fabrik, TimeProvider? zeit = null)
    {
        _fabrik = fabrik;
        _zeit = zeit ?? TimeProvider.System;
    }

    /// <summary>Aktueller Zeitpunkt (lokale Zeit) nach der Uhr des Services.</summary>
    public DateTime Jetzt => _zeit.GetLocalNow().DateTime;

    /// <summary>
    /// Wandelt zwei Kalendertage in einen Zeitraum um und prüft ihn gegen GR01.
    /// </summary>
    /// <param name="vonTag">Erster Tag.</param>
    /// <param name="bisTag">Letzter Tag.</param>
    /// <returns>Zeitraum und Prüfergebnis.</returns>
    public (DateTime Von, DateTime Bis, Validierungsergebnis Ergebnis) PruefeTage(DateTime vonTag, DateTime bisTag)
    {
        var jetzt = Jetzt;
        var (von, bis) = ZeitraumRegel.AusTagen(vonTag, bisTag, jetzt);
        return (von, bis, ZeitraumRegel.Pruefen(von, bis, jetzt));
    }

    /// <summary>Erstellt eine Diagnose für das Profil und den Zeitraum.</summary>
    /// <param name="profil">Das Profil.</param>
    /// <param name="von">Beginn des Zeitraums.</param>
    /// <param name="bis">Ende des Zeitraums.</param>
    /// <param name="fortschritt">Optionale Fortschrittsmeldung in Prozent (0 bis 100).</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    /// <exception cref="ArgumentException">Der Zeitraum verletzt GR01.</exception>
    public async Task<Diagnose> ErstellenAsync(
        Systemprofil profil,
        DateTime von,
        DateTime bis,
        IProgress<int>? fortschritt = null,
        CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(profil);

        var jetzt = Jetzt;
        if (!ZeitraumRegel.Pruefen(von, bis, jetzt).IstGueltig)
        {
            throw new ArgumentException(ZeitraumRegel.Meldung);
        }

        var quellen = _fabrik.Erzeugen(profil);
        fortschritt?.Report(0);

        // Fortschritt der einzelnen Logquellen zu einem Gesamtwert zusammenfassen.
        var anteile = new int[profil.Logquellen.Count];
        void Melde(int index, int prozent)
        {
            anteile[index] = prozent;
            if (anteile.Length > 0)
            {
                fortschritt?.Report(anteile.Sum() * LogAnteil / (100 * anteile.Length));
            }
        }

        // P7: Logs, Dienststatus und Aufgaben werden parallel erfasst.
        var logAufgaben = profil.Logquellen
            .Select((q, i) => LeseQuelleAsync(quellen, q, von, bis, new Progress<int>(p => Melde(i, p)), abbruch))
            .ToList();
        var dienstAufgabe = quellen.Dienste.AbfragenAsync(profil.Dienste, abbruch);
        var aufgabenAufgabe = quellen.Aufgaben.AbfragenAsync(profil.Aufgaben, abbruch);

        await Task.WhenAll([.. logAufgaben, dienstAufgabe, aufgabenAufgabe]);
        fortschritt?.Report(LogAnteil + 10);

        var ergebnisse = logAufgaben.Select(t => t.Result).ToList();
        var eintraege = ergebnisse
            .SelectMany(e => e.Eintraege)
            .OrderBy(e => e.Zeitstempel)
            .ToList();

        // P3: Gruppieren kann bei vielen Einträgen dauern und läuft deshalb nicht im aufrufenden Thread.
        var gruppen = await Task.Run(() => FehlerGruppierer.Gruppiere(eintraege), abbruch);

        var diagnose = new Diagnose
        {
            Profil = profil,
            Von = von,
            Bis = bis,
            ErstelltAm = jetzt,
            Logeintraege = eintraege,
            Fehlergruppen = gruppen,
            Dienste = dienstAufgabe.Result,
            Aufgaben = aufgabenAufgabe.Result,
            GeleseneDateien = [.. ergebnisse.SelectMany(e => e.GeleseneDateien)],
            UebersprungeneDateien = [.. ergebnisse.SelectMany(e => e.Uebersprungen)],
            GeleseneBytes = ergebnisse.Sum(e => e.Bytes),
            IstAbgeschlossen = true,
        };

        fortschritt?.Report(100);
        return diagnose;
    }

    private static async Task<LogleseErgebnis> LeseQuelleAsync(
        Datenquellen quellen, Logquelle quelle, DateTime von, DateTime bis, IProgress<int> fortschritt, CancellationToken abbruch)
    {
        if (quelle.Typ == LogquellenTyp.Ereignisanzeige)
        {
            try
            {
                var ereignisse = await quellen.Ereignisse.LesenAsync(quelle, von, bis, abbruch);
                return new LogleseErgebnis(ereignisse, 0, 0, [], []);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // NFA07: Ein nicht lesbares Protokoll (unbekannter Name, fehlende Rechte) bricht die Analyse nicht ab,
                // sondern erscheint wie eine übersprungene Datei in der Diagnose.
                return new LogleseErgebnis(
                    [], 0, 0, [new UebersprungeneDatei(quelle.Pfad, $"Ereignisprotokoll nicht lesbar: {ex.Message}")], []);
            }
            finally
            {
                fortschritt.Report(100);
            }
        }

        return await quellen.LogLeser.LesenAsync(quelle, von, bis, fortschritt, abbruch);
    }
}
