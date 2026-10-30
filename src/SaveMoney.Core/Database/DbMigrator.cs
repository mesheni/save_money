using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Database;

/// <summary>
/// Миграции схемы через PRAGMA user_version.
/// Каждая новая версия БД — новый case без изменения старых, чтобы обновление с любой версии шло по цепочке.
/// </summary>
public static class DbMigrator
{
    public const int CurrentVersion = 3;

    /// <summary>Эмодзи v1 → ключи иконок v2 (categories.icon). Литералы — ровно как в сидах/палитре v1.</summary>
    private static readonly (string Emoji, string Key)[] EmojiToIconKey =
    [
        // Сид дефолтных категорий
        ("🛒", IconKeys.Groceries), ("🍔", IconKeys.Cafe), ("🚗", IconKeys.Transport),
        ("🏠", IconKeys.Home), ("💊", IconKeys.Health), ("🧹", IconKeys.Household),
        ("👕", IconKeys.Clothes), ("💇", IconKeys.Beauty), ("🎬", IconKeys.Entertainment),
        ("🏃", IconKeys.Sport), ("👶", IconKeys.Kids), ("🎓", IconKeys.Education),
        ("🎁", IconKeys.Gift), ("✈️", IconKeys.Travel), ("🐾", IconKeys.Pets),
        ("💳", IconKeys.Credit), ("🧾", IconKeys.Taxes), ("❓", IconKeys.Other),
        ("💰", IconKeys.Salary), ("🛠️", IconKeys.Freelance), ("🏦", IconKeys.Bank),
        ("↩️", IconKeys.Return), ("🎀", IconKeys.Present),
        // Палитра редактора категорий
        ("🏷", IconKeys.Label), ("🎮", IconKeys.Games), ("☕", IconKeys.Coffee),
        ("🍕", IconKeys.Pizza), ("📚", IconKeys.Books), ("📦", IconKeys.Inventory),
    ];

    public static void Migrate(SQLiteConnection db)
    {
        var version = db.ExecuteScalar<int>("PRAGMA user_version");

        if (version < 1)
        {
            db.CreateTable<Account>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<Category>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<Transaction>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<Debt>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<DebtPayment>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<Budget>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<RecurringPayment>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<MerchantRule>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<NotificationLogEntry>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<Setting>();
        }

        if (version < 2)
        {
            // Данные: эмодзи-иконки категорий → строковые ключи (рендер глифами Material Symbols).
            foreach (var (emoji, key) in EmojiToIconKey)
            {
                db.Execute("UPDATE categories SET icon = ? WHERE icon = ?", key, emoji);
            }

            // 🎁 был и у «Кэшбэк и бонусы» — общее правило отдаёт gift, исправляем на bonus.
            db.Execute("UPDATE categories SET icon = ? WHERE name = ? AND icon = ?",
                IconKeys.Bonus, "Кэшбэк и бонусы", IconKeys.Gift);
        }

        if (version < 3)
        {
            db.CreateTable<Debt>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<DebtPayment>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.CreateTable<Transaction>(CreateFlags.ImplicitPK | CreateFlags.ImplicitIndex);
            db.Execute("UPDATE debts SET kind = ? WHERE kind IS NULL OR kind = ''", DebtKind.Personal);
        }

        if (version > CurrentVersion)
        {
            // Откат версии позволил бы старой сборке перезаписать user_version,
            // и следующий апгрейд повторил бы миграции по уже изменённой схеме.
            throw new NotSupportedException(
                $"База данных (версия {version}) новее, чем поддерживает приложение ({CurrentVersion}). Обновите приложение.");
        }

        if (version < CurrentVersion)
        {
            db.Execute($"PRAGMA user_version = {CurrentVersion}");
        }
    }
}
