namespace Morphix.Core.Domaene;

/// <summary>
/// GR02 und die Feldregeln aus „Eingabevalidierung“ der Spezifikation für ein <see cref="Systemprofil"/>.
/// </summary>
public sealed class ProfilValidator
{
    private const int NameMinLaenge = 3;
    private const int NameMaxLaenge = 50;

    private static readonly char[] UngueltigeMusterZeichen =
        [.. Path.GetInvalidFileNameChars().Where(c => c is not ('*' or '?'))];

    private readonly Func<string, bool> _ordnerIstLesbar;

    /// <summary>Erzeugt den Validator.</summary>
    /// <param name="ordnerIstLesbar">
    /// Prüfung, ob ein Ordner existiert und lesbar ist. Ohne Angabe wird das Dateisystem befragt;
    /// Tests übergeben eine Ersatzfunktion.
    /// </param>
    public ProfilValidator(Func<string, bool>? ordnerIstLesbar = null)
    {
        _ordnerIstLesbar = ordnerIstLesbar ?? OrdnerIstLesbar;
    }

    /// <summary>Prüft ein Profil.</summary>
    /// <param name="profil">Das zu prüfende Profil.</param>
    /// <param name="namenAndererProfile">Namen aller anderen vorhandenen Profile (für die Eindeutigkeit, ohne Groß-/Kleinschreibung).</param>
    public Validierungsergebnis Pruefen(Systemprofil profil, IEnumerable<string> namenAndererProfile)
    {
        ArgumentNullException.ThrowIfNull(profil);
        ArgumentNullException.ThrowIfNull(namenAndererProfile);

        var fehler = new List<Validierungsfehler>();

        var name = profil.Name.Trim();
        var nameOk = name.Length is >= NameMinLaenge and <= NameMaxLaenge
            && !namenAndererProfile.Any(n => string.Equals(n.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (!nameOk)
        {
            fehler.Add(new("Name", "Bitte einen eindeutigen Namen mit 3 bis 50 Zeichen eingeben."));
        }

        if (profil.Logquellen.Count == 0)
        {
            fehler.Add(new("Logquellen", "Bitte mindestens eine Logquelle angeben."));
        }

        for (var i = 0; i < profil.Logquellen.Count; i++)
        {
            PruefeLogquelle(profil.Logquellen[i], i, profil.IstDemo, fehler);
        }

        if (profil.Dienste.Count == 0)
        {
            fehler.Add(new("Dienste", "Bitte mindestens einen Dienst angeben."));
        }

        for (var i = 0; i < profil.Dienste.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(profil.Dienste[i].Dienstname))
            {
                fehler.Add(new($"Dienste[{i}].Dienstname", "Bitte einen Dienstnamen angeben."));
            }
        }

        return fehler.Count == 0 ? Validierungsergebnis.Gueltig : new Validierungsergebnis(fehler);
    }

    private void PruefeLogquelle(Logquelle quelle, int index, bool istDemo, List<Validierungsfehler> fehler)
    {
        if (quelle.Typ == LogquellenTyp.Ereignisanzeige)
        {
            // Bei der Ereignisanzeige ist der „Pfad“ der Protokollname; Ordner und Dateimuster entfallen.
            if (string.IsNullOrWhiteSpace(quelle.Pfad))
            {
                fehler.Add(new($"Logquellen[{index}].Pfad", "Bitte ein Protokoll angeben."));
            }

            return;
        }

        // Demo-Pfade liegen relativ zum Demo-Ordner und existieren nicht im Arbeitsverzeichnis.
        if (string.IsNullOrWhiteSpace(quelle.Pfad) || (!istDemo && !_ordnerIstLesbar(quelle.Pfad)))
        {
            fehler.Add(new($"Logquellen[{index}].Pfad", "Der Ordner existiert nicht oder ist nicht lesbar."));
        }

        var muster = quelle.Dateimuster;
        if (string.IsNullOrWhiteSpace(muster) || muster.IndexOfAny(UngueltigeMusterZeichen) >= 0)
        {
            fehler.Add(new($"Logquellen[{index}].Dateimuster", "Bitte ein Dateimuster wie *.log angeben."));
        }
    }

    private static bool OrdnerIstLesbar(string pfad)
    {
        try
        {
            if (!Directory.Exists(pfad))
            {
                return false;
            }

            // Ein Aufzählungsversuch deckt fehlende Leserechte auf, die Exists nicht erkennt.
            using var e = Directory.EnumerateFileSystemEntries(pfad).GetEnumerator();
            e.MoveNext();
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            return false;
        }
    }
}
