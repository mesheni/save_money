using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Database;

/// <summary>
/// Миграции схемы через PRAGMA user_version.
/// Каждая новая версия БД — новый case без изменения старых, чтобы обновление с любой версии шло по цепочке.
/// </summary>
public static class DbMigrator
{
    public const int CurrentVersion = 1;

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

        // Пример будущей миграции:
        // if (version < 2) { db.Execute("ALTER TABLE transactions ADD COLUMN ..."); }

        if (version != CurrentVersion)
        {
            db.Execute($"PRAGMA user_version = {CurrentVersion}");
        }
    }
}
