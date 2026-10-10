namespace SaveMoney.Core.Models;

/// <summary>
/// Ключи иконок: значения столбца categories.icon и служебных иконок списков
/// (переводы, долги, бюджеты). Раньше там жили эмодзи — теперь строковые ключи;
/// маппинг ключ → глиф Material Symbols находится в head-проекте
/// (Controls/IconKeyConverter), неизвестный ключ рендерится как Other.
/// Длина ключей ограничена столбцом icon (MaxLength 20).
/// </summary>
public static class IconKeys
{
    // Категории расходов
    public const string Groceries = "groceries";
    public const string Cafe = "cafe";
    public const string Transport = "transport";
    public const string Home = "home";
    public const string Health = "health";
    public const string Household = "household";
    public const string Clothes = "clothes";
    public const string Beauty = "beauty";
    public const string Entertainment = "entertainment";
    public const string Sport = "sport";
    public const string Kids = "kids";
    public const string Education = "education";
    public const string Gift = "gift";
    public const string Travel = "travel";
    public const string Pets = "pets";
    public const string Credit = "credit";
    public const string Taxes = "taxes";
    public const string Other = "other";

    // Категории доходов
    public const string Salary = "salary";
    public const string Freelance = "freelance";
    public const string Bonus = "bonus";
    public const string Bank = "bank";
    public const string Return = "return";
    public const string Present = "present";

    // Дополнительная палитра редактора категорий
    public const string Games = "games";
    public const string Coffee = "coffee";
    public const string Pizza = "pizza";
    public const string Books = "books";
    public const string Inventory = "inventory";
    public const string Label = "label";

    // Служебные (не категории)
    public const string Transfer = "transfer";
    public const string DebtOut = "debt_out";
    public const string DebtIn = "debt_in";
    public const string Budgets = "budgets";

    // Счета и меню «Ещё»
    public const string Wallet = "wallet";
    public const string Cash = "cash";
    public const string Category = "category";
    public const string Debts = "debts";
    public const string Payday = "payday";
    public const string Recurring = "recurring";
    public const string Settings = "settings";
    public const string Family = "family";
}
