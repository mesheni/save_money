namespace SaveMoney.Core.Models;

/// <summary>Строковые константы вместо enum — чтобы миграции БД никогда не ломались переименованием.</summary>
public static class AccountKind
{
    public const string Cash = "cash";
    public const string Bank = "bank";
    public const string Card = "card";
}

public static class CategoryKind
{
    public const string Expense = "expense";
    public const string Income = "income";
}

public static class TransactionKind
{
    public const string Expense = "expense";
    public const string Income = "income";
    public const string Transfer = "transfer";
}

public static class TransactionSource
{
    public const string Manual = "manual";
    public const string Widget = "widget";
    public const string Notification = "notification";
    public const string Import = "import";
}

public static class DebtDirection
{
    /// <summary>Мне должны.</summary>
    public const string OwedToMe = "owed_to_me";

    /// <summary>Я должен.</summary>
    public const string IOwe = "i_owe";
}

public static class DebtStatus
{
    public const string Active = "active";
    public const string Settled = "settled";
}
