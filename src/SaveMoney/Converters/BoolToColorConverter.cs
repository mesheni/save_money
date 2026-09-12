using System.Globalization;
using Microsoft.Maui.Graphics;

namespace SaveMoney.Converters;

/// <summary>Bool → Color: для подсветки выбранных чипов (категории, счета, иконки).</summary>
public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.White;
    public Color FalseColor { get; set; } = Colors.Black;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? TrueColor : FalseColor;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
