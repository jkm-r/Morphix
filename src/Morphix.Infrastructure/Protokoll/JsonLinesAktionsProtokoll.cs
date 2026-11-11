using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Morphix.Core.Domaene;
using Morphix.Core.Schnittstellen;

namespace Morphix.Infrastructure.Protokoll;

/// <summary>
/// Aktionsprotokoll als JSON-Lines-Datei (eine Zeile je Aktion), standardmäßig <c>%AppData%\Morphix\aktionen.jsonl</c> (US16).
/// Die Datei wird nur angehängt, nie umgeschrieben oder gekürzt. Das Protokoll bleibt auf dem Rechner (NFA06).
/// </summary>
public sealed class JsonLinesAktionsProtokoll : IAktionsProtokoll
{
    private static readonly JsonSerializerOptions Optionen = new()
    {
        // Umlaute bleiben lesbar; Aufzählungen stehen als Text („Stoppen“) in der Datei.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _pfad;
    private readonly SemaphoreSlim _sperre = new(1, 1);

    /// <summary>Erzeugt das Protokoll.</summary>
    /// <param name="pfad">Pfad der Protokolldatei; der Ordner wird bei Bedarf angelegt.</param>
    public JsonLinesAktionsProtokoll(string pfad)
    {
        _pfad = pfad;
    }

    /// <inheritdoc />
    public async Task ProtokolliereAsync(Dienstaktion aktion, CancellationToken abbruch = default)
    {
        ArgumentNullException.ThrowIfNull(aktion);

        // Eine Zeile je Eintrag; Zeilenumbrüche in Texten würden das Format zerstören (JSON maskiert sie ohnehin als \n).
        var zeile = JsonSerializer.Serialize(aktion, Optionen) + "\n";

        await _sperre.WaitAsync(abbruch);
        try
        {
            var ordner = Path.GetDirectoryName(_pfad);
            if (!string.IsNullOrEmpty(ordner))
            {
                Directory.CreateDirectory(ordner);
            }

            await using var datei = new FileStream(_pfad, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
            await datei.WriteAsync(new UTF8Encoding(false).GetBytes(zeile), abbruch);
        }
        finally
        {
            _sperre.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Dienstaktion>> LeseAsync(int maximal = 100, CancellationToken abbruch = default)
    {
        if (maximal <= 0 || !File.Exists(_pfad))
        {
            return [];
        }

        await _sperre.WaitAsync(abbruch);
        try
        {
            var eintraege = new List<Dienstaktion>();
            await using var datei = new FileStream(_pfad, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
            using var leser = new StreamReader(datei, Encoding.UTF8);
            while (await leser.ReadLineAsync(abbruch) is { } zeile)
            {
                if (string.IsNullOrWhiteSpace(zeile))
                {
                    continue;
                }

                try
                {
                    if (JsonSerializer.Deserialize<Dienstaktion>(zeile, Optionen) is { } eintrag)
                    {
                        eintraege.Add(eintrag);
                    }
                }
                catch (JsonException)
                {
                    // Eine beschädigte Zeile (z. B. nach einem Absturz beim Schreiben) darf das Lesen der übrigen nicht verhindern.
                }
            }

            eintraege.Reverse();
            return eintraege.Count > maximal ? eintraege[..maximal] : eintraege;
        }
        finally
        {
            _sperre.Release();
        }
    }
}
