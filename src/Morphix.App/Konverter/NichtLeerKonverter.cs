using System.Globalization;
using System.Windows.Data;

namespace Morphix.App.Konverter;

/// <summary>Wandelt einen Text in <c>true</c>, wenn er nicht leer ist. Dient dazu, Felder mit Fehlertext rot zu markieren.</summary>
public sealed class NichtLeerKonverter : IValueConverter
{
    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        !string.IsNullOrEmpty(value as string);

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
