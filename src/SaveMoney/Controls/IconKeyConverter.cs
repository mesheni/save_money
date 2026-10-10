using System.Globalization;
using SaveMoney.Core.Models;

namespace SaveMoney.Controls;

/// <summary>
/// Ключ иконки (IconKeys, столбец categories.icon) → глиф Material Symbols.
/// Используется в XAML: Label Text="{Binding Icon, Converter={StaticResource IconGlyph}}"
/// с FontFamily="MaterialSymbolsRounded" — иконка красится TextColor, как обычный текст.
/// </summary>
public sealed class IconKeyConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Map = new()
    {
        // Категории расходов
        [IconKeys.Groceries] = Glyphs.ShoppingCart,
        [IconKeys.Cafe] = Glyphs.Restaurant,
        [IconKeys.Transport] = Glyphs.DirectionsCar,
        [IconKeys.Home] = Glyphs.Home,
        [IconKeys.Health] = Glyphs.Medication,
        [IconKeys.Household] = Glyphs.CleaningServices,
        [IconKeys.Clothes] = Glyphs.Checkroom,
        [IconKeys.Beauty] = Glyphs.ContentCut,
        [IconKeys.Entertainment] = Glyphs.Movie,
        [IconKeys.Sport] = Glyphs.DirectionsRun,
        [IconKeys.Kids] = Glyphs.ChildCare,
        [IconKeys.Education] = Glyphs.School,
        [IconKeys.Gift] = Glyphs.Redeem,
        [IconKeys.Travel] = Glyphs.Flight,
        [IconKeys.Pets] = Glyphs.Pets,
        [IconKeys.Credit] = Glyphs.CreditCard,
        [IconKeys.Taxes] = Glyphs.ReceiptLong,
        // Категории доходов и счета
        [IconKeys.Salary] = Glyphs.Payments,
        [IconKeys.Freelance] = Glyphs.Handyman,
        [IconKeys.Bonus] = Glyphs.Loyalty,
        [IconKeys.Bank] = Glyphs.AccountBalance,
        [IconKeys.Return] = Glyphs.Undo,
        [IconKeys.Present] = Glyphs.Favorite,
        // Палитра редактора категорий
        [IconKeys.Games] = Glyphs.SportsEsports,
        [IconKeys.Coffee] = Glyphs.LocalCafe,
        [IconKeys.Pizza] = Glyphs.LocalPizza,
        [IconKeys.Books] = Glyphs.MenuBook,
        [IconKeys.Inventory] = Glyphs.Inventory2,
        [IconKeys.Label] = Glyphs.Label,
        // Служебные
        [IconKeys.Transfer] = Glyphs.SwapHoriz,
        [IconKeys.DebtOut] = Glyphs.NorthEast,
        [IconKeys.DebtIn] = Glyphs.SouthWest,
        [IconKeys.Budgets] = Glyphs.Flag,
        // Счета и меню «Ещё»
        [IconKeys.Wallet] = Glyphs.Wallet,
        [IconKeys.Cash] = Glyphs.Payments,
        [IconKeys.Category] = Glyphs.Category,
        [IconKeys.Debts] = Glyphs.Handshake,
        [IconKeys.Payday] = Glyphs.Calendar,
        [IconKeys.Recurring] = Glyphs.Repeat,
        [IconKeys.Settings] = Glyphs.Settings,
        [IconKeys.Family] = Glyphs.Group,
        // Fallback
        [IconKeys.Other] = Glyphs.Info,
    };

    /// <summary>Глиф для ключа; неизвестный ключ — как Other.</summary>
    public static string ToGlyph(string? key)
        => key is not null && Map.TryGetValue(key, out var glyph) ? glyph : Map[IconKeys.Other];

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => ToGlyph(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Только прямое преобразование ключ → глиф.");
}
