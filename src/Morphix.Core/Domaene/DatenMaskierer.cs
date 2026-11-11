using System.Text.RegularExpressions;

namespace Morphix.Core.Domaene;

/// <summary>
/// Macht sensible Angaben in Texten unkenntlich (US15, GR10): IPv4-Adressen und Benutzernamen.
/// </summary>
public sealed partial class DatenMaskierer
{
    /// <summary>Ersatz für eine IPv4-Adresse.</summary>
    public const string IpErsatz = "***.***.***.***";

    /// <summary>Ersatz für einen Benutzernamen.</summary>
    public const string BenutzerErsatz = "[Benutzer]";

    private readonly Regex? _aktuellerBenutzer;

    /// <summary>Erzeugt den Maskierer.</summary>
    /// <param name="aktuellerBenutzer">
    /// Name des angemeldeten Benutzers, der überall im Text maskiert wird. Ohne Angabe wird
    /// <see cref="Environment.UserName"/> verwendet; ein leerer Text schaltet diese Regel ab.
    /// </param>
    public DatenMaskierer(string? aktuellerBenutzer = null)
    {
        var name = (aktuellerBenutzer ?? Environment.UserName).Trim();
        if (name.Length > 0)
        {
            // Nur ganze Wörter, damit ein kurzer Benutzername nicht mitten in anderen Wörtern ersetzt wird.
            _aktuellerBenutzer = new Regex(
                $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(name)}(?![\p{{L}}\p{{N}}_])",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }

    /// <summary>Maskiert IP-Adressen und Benutzernamen in einem Text.</summary>
    public string Maskiere(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Domänenform zuerst: Sie enthält den Benutzernamen und soll als Ganzes ersetzt werden.
        var ergebnis = DomaeneBenutzerRegex().Replace(text, BenutzerErsatz);
        ergebnis = UserGleichRegex().Replace(ergebnis, "${schluessel}" + BenutzerErsatz);
        ergebnis = BenutzerDoppelpunktRegex().Replace(ergebnis, "${schluessel}" + BenutzerErsatz);

        if (_aktuellerBenutzer is not null)
        {
            ergebnis = _aktuellerBenutzer.Replace(ergebnis, BenutzerErsatz);
        }

        return Ipv4Regex().Replace(ergebnis, IpErsatz);
    }

    // DOMAENE\name. Der Lookbehind verhindert Treffer innerhalb von Pfaden wie C:\Temp\x.log oder \\server\share.
    [GeneratedRegex(@"(?<![\w\\.:\-])[A-Za-z][A-Za-z0-9_\-]*\\[A-Za-z0-9_](?:[A-Za-z0-9._\-]*[A-Za-z0-9_])?")]
    private static partial Regex DomaeneBenutzerRegex();

    [GeneratedRegex(@"(?<schluessel>\buser=)(?!\[Benutzer\])[^\s,;]+", RegexOptions.IgnoreCase)]
    private static partial Regex UserGleichRegex();

    [GeneratedRegex(@"(?<schluessel>\bBenutzer:\s*)(?!\[Benutzer\])[^\s,;]+", RegexOptions.IgnoreCase)]
    private static partial Regex BenutzerDoppelpunktRegex();

    [GeneratedRegex(@"\b(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)\b")]
    private static partial Regex Ipv4Regex();
}
