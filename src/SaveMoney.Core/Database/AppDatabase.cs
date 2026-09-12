using SaveMoney.Core.Models;
using SaveMoney.Core.Repositories;
using SQLite;

namespace SaveMoney.Core.Database;

/// <summary>
/// Точка доступа к SQLite: открывает соединение, накатывает миграции, сеет дефолтные данные.
/// Все репозитории работают через <see cref="Connection"/>.
/// </summary>
public sealed class AppDatabase : IDisposable
{
    private readonly SQLiteConnection _db;

    public SQLiteConnection Connection => _db;

    public AppDatabase(string path)
    {
        // sqlite-net-pcl 1.11.x сам инициализирует SQLitePCLRaw-провайдер.
        _db = new SQLiteConnection(
            new SQLiteConnectionString(path, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache, storeDateTimeAsTicks: true));
        DbMigrator.Migrate(_db);
        DefaultCategories.SeedIfNeeded(this);
    }

    public AccountRepository Accounts => new(_db);
    public CategoryRepository Categories => new(_db);
    public TransactionRepository Transactions => new(_db);
    public DebtRepository Debts => new(_db);
    public MerchantRuleRepository MerchantRules => new(_db);
    public SettingRepository Settings => new(_db);
    public BudgetRepository Budgets => new(_db);
    public RecurringPaymentRepository RecurringPayments => new(_db);

    public void Dispose() => _db.Dispose();
}
