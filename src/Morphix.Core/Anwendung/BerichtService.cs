using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Core.Anwendung;

/// <summary>
/// Steuert den Prozess P7 „Support-Bericht erstellen“: erzeugt den HTML-Bericht aus einer abgeschlossenen
/// Diagnose (GR09), maskiert sensible Daten (GR10, US15) und löst den Export als ZIP aus (US14).
/// Die Maskierung läuft hier, bevor etwas an den Exporter geht; ungeschützte Daten verlassen den Rechner
/// dadurch nicht, solange der Anwender die Maskierung nicht bewusst abschaltet.
/// </summary>
public sealed class BerichtService
{
    /// <summary>Meldung aus der Eingabevalidierung der Spezifikation („Exportordner“).</summary>
    public const string ExportordnerMeldung = "In diesen Ordner kann nicht geschrieben werden.";

    /// <summary>Meldung bei GR09.</summary>
    public const string KeineDiagnoseMeldung = "Ein Bericht kann nur aus einer abgeschlossenen Diagnose erzeugt werden.";

    private readonly IBerichtExporter _exporter;
    private readonly TimeProvider _zeit;
    private readonly Systeminfo _system;
    private readonly DatenMaskierer _maskierer;

    /// <summary>Erzeugt den Service.</summary>
    /// <param name="exporter">Schreibt die ZIP-Datei.</param>
    /// <param name="zeit">Uhr; Tests übergeben eine feste Zeit. Ohne Angabe gilt die Systemuhr.</param>
    /// <param name="system">Rechnerangaben; ohne Angabe die des laufenden Rechners.</param>
    /// <param name="maskierer">Maskierung; ohne Angabe ein <see cref="DatenMaskierer"/> für den angemeldeten Benutzer.</param>
    public BerichtService(
        IBerichtExporter exporter,
        TimeProvider? zeit = null,
        Systeminfo? system = null,
        DatenMaskierer? maskierer = null)
    {
        _exporter = exporter;
        _zeit = zeit ?? TimeProvider.System;
        _system = system ?? Systeminfo.Aktuell();
        _maskierer = maskierer ?? new DatenMaskierer();
    }

    /// <summary>
    /// Erzeugt den Bericht als vollständiges HTML-Dokument. Alle Inhalte sind HTML-kodiert.
    /// </summary>
    /// <param name="diagnose">Die abgeschlossene Diagnose.</param>
    /// <param name="maskieren">Ob IP-Adressen, Benutzernamen und der Rechnername unkenntlich gemacht werden (GR10).</param>
    /// <exception cref="InvalidOperationException">Die Diagnose ist nicht abgeschlossen (GR09).</exception>
    public string Erzeugen(Diagnose diagnose, bool maskieren = true)
    {
        ArgumentNullException.ThrowIfNull(diagnose);
        if (!diagnose.IstAbgeschlossen)
        {
            throw new InvalidOperationException(KeineDiagnoseMeldung);
        }

        var jetzt = _zeit.GetLocalNow().DateTime;
        return new HtmlBerichtErzeuger(diagnose, _system, jetzt, maskieren, _maskierer.Maskiere).Erzeugen();
    }

    /// <summary>Schlägt einen Dateinamen vor: <c>Morphix_&lt;Profil&gt;_&lt;yyyyMMdd_HHmm&gt;.zip</c>.</summary>
    public string Dateinamenvorschlag(Diagnose diagnose)
    {
        ArgumentNullException.ThrowIfNull(diagnose);

        var ungueltig = Path.GetInvalidFileNameChars();
        var profil = new string([.. diagnose.Profil.Name.Select(z => ungueltig.Contains(z) || char.IsWhiteSpace(z) ? '_' : z)]);
        while (profil.Contains("__", StringComparison.Ordinal))
        {
            profil = profil.Replace("__", "_", StringComparison.Ordinal);
        }

        profil = profil.Trim('_', '.');
        if (profil.Length == 0)
        {
            profil = "Profil";
        }

        return $"Morphix_{profil}_{_zeit.GetLocalNow():yyyyMMdd_HHmm}.zip";
    }

    /// <summary>Prüft, ob am Zielpfad geschrieben werden kann.</summary>
    /// <returns>Die Meldung <see cref="ExportordnerMeldung"/> oder <c>null</c>, wenn alles in Ordnung ist.</returns>
    public string? PruefeZielpfad(string zielpfad) =>
        !string.IsNullOrWhiteSpace(zielpfad) && _exporter.KannSchreiben(zielpfad) ? null : ExportordnerMeldung;

    /// <summary>
    /// Erzeugt den Bericht und schreibt ihn mit Kopien der gelesenen Logdateien als ZIP. Die Originale bleiben
    /// unangetastet (GR05). Ist die Maskierung aktiv, gilt sie auch für die Kopien der Logdateien im ZIP.
    /// </summary>
    /// <param name="diagnose">Die abgeschlossene Diagnose.</param>
    /// <param name="maskieren">Ob sensible Daten maskiert werden (GR10).</param>
    /// <param name="zielpfad">Pfad der ZIP-Datei.</param>
    /// <param name="abbruch">Abbruchsignal.</param>
    /// <returns>Der erzeugte <see cref="SupportBericht"/> mit dem Pfad der Datei.</returns>
    /// <exception cref="InvalidOperationException">Die Diagnose ist nicht abgeschlossen (GR09).</exception>
    /// <exception cref="IOException">Die Datei konnte nicht geschrieben werden.</exception>
    public async Task<SupportBericht> ExportierenAsync(
        Diagnose diagnose, bool maskieren, string zielpfad, CancellationToken abbruch = default)
    {
        var html = Erzeugen(diagnose, maskieren);
        await _exporter.ExportierenAsync(
            html, diagnose.GeleseneDateien, zielpfad, maskieren ? _maskierer.Maskiere : null, abbruch);
        return new SupportBericht(_zeit.GetLocalNow().DateTime, maskieren, zielpfad);
    }
}
