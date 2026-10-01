namespace SaveMoney.Controls;

/// <summary>
/// Глифы Material Symbols Rounded (субсет MaterialSymbolsRounded-sub.ttf, Apache 2.0).
/// В шрифт включены только эти символы — при добавлении новых глифов пересобрать субсет.
/// </summary>
public static class Glyphs
{
    public const string Wallet = "\ue850";        // account_balance_wallet
    public const string Add = "\ue145";
    public const string Apps = "\ue5c3";
    public const string ArrowBack = "\ue5c4";
    public const string Calendar = "\uebcc";      // calendar_month
    public const string Check = "\ue668";
    public const string ChevronRight = "\ue5cc";
    public const string Close = "\ue5cd";
    public const string Delete = "\ue92e";
    public const string DonutSmall = "\ue918";
    public const string Download = "\uf090";
    public const string EditSquare = "\uf88d";
    public const string Error = "\uf8b6";
    public const string Info = "\ue88e";
    public const string MoreHoriz = "\ue5d3";
    public const string ReceiptLong = "\uef6e";
    public const string Repeat = "\ue040";
    public const string Savings = "\ue2eb";
    public const string Schedule = "\uefd6";
    public const string Search = "\uef7a";
    public const string Share = "\ue80d";
    public const string Sync = "\ue627";
    public const string TrendingUp = "\ue8e5";
    public const string Tune = "\ue429";

    /// <summary>Иконка из субсета Material Symbols.</summary>
    public static FontImageSource Icon(string glyph, double size = 22, Color? color = null) => new()
    {
        Glyph = glyph,
        FontFamily = "MaterialSymbolsRounded",
        Size = size,
        Color = color ?? Colors.Black,
    };
}
