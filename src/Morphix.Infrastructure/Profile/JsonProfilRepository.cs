using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Profile;

/// <summary>
/// Speichert Systemprofile als JSON-Dateien (eine Datei je Profil) und merkt sich das zuletzt genutzte Profil.
/// Alle Pfade werden von außen übergeben, damit Tests mit temporären Ordnern arbeiten können.
/// </summary>
public sealed class JsonProfilRepository : IProfilRepository
{
    private static readonly JsonSerializerOptions Optionen = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _profilOrdner;
    private readonly string _einstellungsDatei;
    private readonly string _demoProfilDatei;

    /// <summary>Erzeugt das Repository. Es werden keine Ordner angelegt, bevor etwas gespeichert wird.</summary>
    /// <param name="profilOrdner">Ordner der Profildateien, üblicherweise <c>%AppData%\Morphix\Profile</c>.</param>
    /// <param name="einstellungsDatei">Datei für das zuletzt genutzte Profil.</param>
    /// <param name="demoProfilDatei">Mitgeliefertes Demo-Profil (<c>demo\demo-profil.json</c>).</param>
    public JsonProfilRepository(string profilOrdner, string einstellungsDatei, string demoProfilDatei)
    {
        _profilOrdner = profilOrdner;
        _einstellungsDatei = einstellungsDatei;
        _demoProfilDatei = demoProfilDatei;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Systemprofil>> LadenAlleAsync(CancellationToken abbruch = default)
    {
        var ergebnis = new List<Systemprofil>();
        if (!Directory.Exists(_profilOrdner))
        {
            return ergebnis;
        }

        foreach (var datei in Directory.EnumerateFiles(_profilOrdner, "*.json"))
        {
            var profil = await LeseProfilAsync(datei, abbruch);
            if (profil is not null)
            {
                ergebnis.Add(profil);
            }
        }

        return ergebnis;
    }

    /// <inheritdoc />
    public async Task SpeichernAsync(Systemprofil profil, string? alterName = null, CancellationToken abbruch = default)
    {
        Directory.CreateDirectory(_profilOrdner);

        // Erst in eine temporäre Datei schreiben, dann ersetzen: bricht das Schreiben ab,
        // bleibt die bisherige Profildatei intakt.
        var ziel = DateiFuer(profil.Name);
        var temp = ziel + ".tmp";
        await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, profil, Optionen, abbruch);
        }

        File.Move(temp, ziel, overwrite: true);

        if (alterName is not null && !string.Equals(alterName, profil.Name, StringComparison.Ordinal))
        {
            File.Delete(DateiFuer(alterName));
        }
    }

    /// <inheritdoc />
    public Task LoeschenAsync(string name, CancellationToken abbruch = default)
    {
        File.Delete(DateiFuer(name));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<string?> LadeZuletztGenutztesAsync(CancellationToken abbruch = default)
    {
        try
        {
            await using var stream = File.OpenRead(_einstellungsDatei);
            var e = await JsonSerializer.DeserializeAsync<Einstellungen>(stream, Optionen, abbruch);
            return e?.ZuletztGenutztesProfil;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task MerkeZuletztGenutztesAsync(string name, CancellationToken abbruch = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_einstellungsDatei)!);
        await using var stream = new FileStream(_einstellungsDatei, FileMode.Create, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(stream, new Einstellungen { ZuletztGenutztesProfil = name }, Optionen, abbruch);
    }

    /// <inheritdoc />
    public Task<Systemprofil?> LadeDemoProfilAsync(CancellationToken abbruch = default) =>
        LeseProfilAsync(_demoProfilDatei, abbruch);

    /// <summary>
    /// Dateiname eines Profils: der Profilname, bei ungültigen Zeichen zusätzlich mit Kurz-Hash,
    /// damit zwei verschiedene Namen nie dieselbe Datei ergeben.
    /// </summary>
    internal string DateiFuer(string name)
    {
        var sauber = string.Concat(name.Select(c => Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c)).Trim();
        if (sauber != name || sauber.Length == 0)
        {
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8];
            sauber = $"{sauber}_{hash}";
        }

        return Path.Combine(_profilOrdner, sauber + ".json");
    }

    private static async Task<Systemprofil?> LeseProfilAsync(string datei, CancellationToken abbruch)
    {
        try
        {
            await using var stream = new FileStream(datei, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return await JsonSerializer.DeserializeAsync<Systemprofil>(stream, Optionen, abbruch);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null; // Defekte oder gesperrte Profildatei: überspringen, andere Profile bleiben nutzbar (NFA07).
        }
    }

    private sealed class Einstellungen
    {
        public string? ZuletztGenutztesProfil { get; set; }
    }
}
