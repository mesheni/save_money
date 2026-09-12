using System.Globalization;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Graphics;

namespace SaveMoney.Converters;

/// <summary>
/// Bool → Color с учётом темы приложения.
/// Style.Triggers с DataTrigger не работают в ResourceDictionary под SourceGen-инфлятором,
/// поэтому выборные состояния (чипы, суммы) делаются конвертером; тёмные пары — опциональны.
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.White;
    public Color FalseColor { get; set; } = Colors.Black;

    /// <summary>Используются в тёмной теме; null — берётся основная пара.</summary>
    public Color? TrueColorDark { get; set; }
    public Color? FalseColorDark { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isTrue = value is true;
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;

        if (dark)
        {
            var trueDark = TrueColorDark ?? TrueColor;
            var falseDark = FalseColorDark ?? FalseColor;
            return isTrue ? trueDark : falseDark;
        }

        return isTrue ? TrueColor : FalseColor;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
