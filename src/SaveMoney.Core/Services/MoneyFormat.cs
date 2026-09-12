using System.Globalization;

namespace SaveMoney.Core.Services;

/// <summary>
/// Форматирование сумм из копеек и парсинг пользовательского ввода.
/// Ввод: только запятая как разделитель (русская раскладка), парсинг терпит и точку.
/// </summary>
public static class MoneyFormat
{
    public static string Rubles(long minorUnits)
    {
        var rubles = minorUnits / 100m;
        return $"{rubles:N2} ₽";
    }

    public static string SignedRubles(long minorUnits)
    {
        var prefix = minorUnits > 0 ? "+" : string.Empty;
        return prefix + Rubles(minorUnits);
    }

    /// <summary>Копейки → текст для поля ввода: 150000 → "1500", 15550 → "155,5".</summary>
    public static string ForInput(long minorUnits)
    {
        if (minorUnits == 0)
        {
            return "";
        }

        var rubles = minorUnits / 100m;
        return rubles % 1 == 0
            ? ((long)rubles).ToString(CultureInfo.InvariantCulture)
            : rubles.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    /// <summary>Текст ввода → копейки. Пустое/некорректное → 0. "1 500,50" → 150050.</summary>
    public static long ParseInput(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var normalized = text.Replace(" ", "").Replace("\u00a0", "").Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        return (long)Math.Round(value * 100m);
    }
}
