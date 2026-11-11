using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Morphix.Core.Domaene;

namespace Morphix.App.ViewModels;

/// <summary>
/// Basis der Listenzeilen im Dialog „Profil bearbeiten“. Meldet Änderungen an Eingabefeldern über
/// <see cref="Geaendert"/>, damit das Dialog-ViewModel neu validiert. Fehlertexte (Eigenschaften, die auf
/// „Fehler“ enden) lösen das nicht aus.
/// </summary>
public abstract class ProfilZeile : ObservableObject
{
    /// <summary>Wird ausgelöst, wenn der Anwender ein Eingabefeld ändert.</summary>
    public event Action? Geaendert;

    /// <inheritdoc />
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName is not null && !e.PropertyName.EndsWith("Fehler", StringComparison.Ordinal))
        {
            Geaendert?.Invoke();
        }
    }
}

/// <summary>Bearbeitbare Zeile einer <see cref="Logquelle"/>.</summary>
public sealed partial class LogquelleZeile : ProfilZeile
{
    private readonly Logquelle _original;

    /// <summary>Erzeugt die Zeile aus einer Logquelle.</summary>
    public LogquelleZeile(Logquelle quelle)
    {
        _original = quelle;
        Pfad = quelle.Pfad;
        Dateimuster = quelle.Dateimuster;
        Typ = quelle.Typ;
    }

    /// <summary>Ordnerpfad bzw. Protokollname.</summary>
    [ObservableProperty]
    public partial string Pfad { get; set; }

    /// <summary>Dateimuster, z. B. <c>*.log</c>.</summary>
    [ObservableProperty]
    public partial string Dateimuster { get; set; }

    /// <summary>Art der Quelle.</summary>
    [ObservableProperty]
    public partial LogquellenTyp Typ { get; set; }

    /// <summary>Fehlertext zum Pfad (leer, wenn gültig).</summary>
    [ObservableProperty]
    public partial string PfadFehler { get; set; } = "";

    /// <summary>Fehlertext zum Dateimuster (leer, wenn gültig).</summary>
    [ObservableProperty]
    public partial string DateimusterFehler { get; set; } = "";

    /// <summary>Wandelt die Zeile zurück; nicht bearbeitbare Eigenschaften (z. B. das Zeilenmuster) bleiben erhalten.</summary>
    public Logquelle ZuLogquelle() => _original with { Pfad = Pfad, Dateimuster = Dateimuster, Typ = Typ };
}

/// <summary>Bearbeitbare Zeile eines <see cref="Dienst"/>.</summary>
public sealed partial class DienstZeile : ProfilZeile
{
    private readonly Dienst _original;

    /// <summary>Erzeugt die Zeile aus einem Dienst.</summary>
    public DienstZeile(Dienst dienst)
    {
        _original = dienst;
        Dienstname = dienst.Dienstname;
        Anzeigename = dienst.Anzeigename;
        IstKritisch = dienst.IstKritisch;
    }

    /// <summary>Technischer Dienstname.</summary>
    [ObservableProperty]
    public partial string Dienstname { get; set; }

    /// <summary>Anzeigename (optional).</summary>
    [ObservableProperty]
    public partial string Anzeigename { get; set; }

    /// <summary>Kennzeichnet einen kritischen Dienst.</summary>
    [ObservableProperty]
    public partial bool IstKritisch { get; set; }

    /// <summary>Fehlertext zum Dienstnamen (leer, wenn gültig).</summary>
    [ObservableProperty]
    public partial string DienstnameFehler { get; set; } = "";

    /// <summary>Wandelt die Zeile zurück; Status und Starttyp des Profils bleiben erhalten.</summary>
    public Dienst ZuDienst() => _original with { Dienstname = Dienstname, Anzeigename = Anzeigename, IstKritisch = IstKritisch };
}

/// <summary>Bearbeitbare Zeile einer <see cref="GeplanteAufgabe"/>.</summary>
public sealed partial class AufgabeZeile : ProfilZeile
{
    private readonly GeplanteAufgabe _original;

    /// <summary>Erzeugt die Zeile aus einer Aufgabe.</summary>
    public AufgabeZeile(GeplanteAufgabe aufgabe)
    {
        _original = aufgabe;
        Name = aufgabe.Name;
    }

    /// <summary>Name der Aufgabe in der Aufgabenplanung.</summary>
    [ObservableProperty]
    public partial string Name { get; set; }

    /// <summary>Wandelt die Zeile zurück.</summary>
    public GeplanteAufgabe ZuAufgabe() => _original with { Name = Name };
}
