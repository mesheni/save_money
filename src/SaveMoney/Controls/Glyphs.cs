namespace SaveMoney.Controls;

/// <summary>
/// Глифы Material Symbols Rounded (субсет MaterialSymbolsRounded-sub.ttf, Apache 2.0).
/// В шрифт включены только эти символы — при добавлении новых глифов пересобрать субсет:
/// python tools/subset-material-symbols.py (он же печатает актуальные codepoints).
/// Google меняет codepoints между версиями шрифта — сверяйте с выводом скрипта.
/// </summary>
public static class Glyphs
{
    public const string Wallet = "\ue850";        // account_balance_wallet
    public const string Add = "\ue145";
    public const string Apps = "\ue5c3";
    public const string ArrowBack = "\ue5c4";
    public const string Calendar = "\uebcc";      // calendar_month
    public const string Check = "\ue5ca";
    public const string ChevronRight = "\ue409";
    public const string Close = "\ue14c";
    public const string Delete = "\ue872";
    public const string DonutSmall = "\ue918";
    public const string Download = "\ue171";
    public const string EditSquare = "\uf88d";
    public const string Error = "\ue000";
    public const string Info = "\ue88e";
    public const string MoreHoriz = "\ue5d3";
    public const string ReceiptLong = "\uef6e";
    public const string Repeat = "\ue040";
    public const string Savings = "\ue2eb";
    public const string Schedule = "\ue192";
    public const string Search = "\ue8b6";
    public const string Share = "\ue80d";
    public const string Sync = "\ue627";
    public const string TrendingUp = "\ue8e5";
    public const string Tune = "\ue429";

    // Категории расходов
    public const string ShoppingCart = "\ue547";  // продукты
    public const string Restaurant = "\ue56c";    // кафе
    public const string DirectionsCar = "\ue531"; // транспорт
    public const string Home = "\ue88a";          // ЖКХ
    public const string Medication = "\uf033";    // здоровье
    public const string CleaningServices = "\uf0ff"; // дом и быт
    public const string Checkroom = "\uf19e";     // одежда
    public const string ContentCut = "\ue14e";    // уход за собой
    public const string Movie = "\ue02c";         // развлечения
    public const string DirectionsRun = "\ue566"; // спорт
    public const string ChildCare = "\ueb41";     // дети
    public const string School = "\ue80c";        // образование
    public const string Redeem = "\ue8b1";        // подарки
    public const string Flight = "\ue539";        // путешествия
    public const string Pets = "\ue91d";          // питомцы
    public const string CreditCard = "\ue870";    // кредиты

    // Категории доходов и счета
    public const string Payments = "\uef63";      // зарплата, наличные
    public const string Handyman = "\uf10b";      // подработка
    public const string Loyalty = "\ue89a";       // кэшбэк и бонусы
    public const string AccountBalance = "\ue84f";// вклады, банковский счёт
    public const string Undo = "\ue166";          // возврат долга
    public const string Favorite = "\ue87d";      // подарки полученные

    // Дополнительная палитра редактора категорий
    public const string SportsEsports = "\ue6ec";
    public const string LocalCafe = "\ue541";
    public const string LocalPizza = "\ue552";
    public const string MenuBook = "\uea19";
    public const string Inventory2 = "\ue1a1";
    public const string Label = "\ue892";

    // Служебные
    public const string SwapHoriz = "\ue8d4";     // переводы
    public const string NorthEast = "\uf1e1";     // долг: я дал
    public const string SouthWest = "\uf1e5";     // долг: мне должны
    public const string Category = "\ue574";
    public const string Handshake = "\uebcb";     // долги
    public const string Flag = "\ue153";          // бюджеты
    public const string Group = "\ue7ef";         // семья
    public const string Settings = "\ue8b8";
    public const string Warning = "\ue002";

    /// <summary>Иконка из субсета Material Symbols.</summary>
    public static FontImageSource Icon(string glyph, double size = 22, Color? color = null) => new()
    {
        Glyph = glyph,
        FontFamily = "MaterialSymbolsRounded",
        Size = size,
        Color = color ?? Colors.Black,
    };
}
