namespace SaveMoney.Core.Services;

/// <summary>Форматирование сумм из копеек. Культура не зависит от устройства — всегда ru-RU стиль «1 234,56 ₽».</summary>
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
}
