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
    private readonly string _path;
    private SQLiteConnection _db;

    /// <summary>Текущее соединение. После <see cref="RestoreFrom"/> — новое: не кешируйте его.</summary>
    public SQLiteConnection Connection => _db;

    public AppDatabase(string path)
    {
        _path = path;
        _db = Open(path);
        DbMigrator.Migrate(_db);
        DefaultCategories.SeedIfNeeded(this);
    }

    // sqlite-net-pcl 1.11.x сам инициализирует SQLitePCLRaw-провайдер.
    private static SQLiteConnection Open(string path) =>
        new(new SQLiteConnectionString(path,
            SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.SharedCache, storeDateTimeAsTicks: true));

    /// <summary>
    /// Проверяет, что файл — резервная копия SaveMoney подходящей версии.
    /// Возвращает текст ошибки для пользователя или null, если файл годится.
    /// </summary>
    public static string? ValidateBackup(string backupPath)
    {
        try
        {
            using var probe = new SQLiteConnection(
                new SQLiteConnectionString(backupPath, SQLiteOpenFlags.ReadOnly, storeDateTimeAsTicks: true));
            var version = probe.ExecuteScalar<int>("PRAGMA user_version");
            if (version > DbMigrator.CurrentVersion)
            {
                return "Копия создана более новой версией приложения — обновите SaveMoney.";
            }

            var tables = probe.QueryScalars<string>("SELECT name FROM sqlite_master WHERE type = 'table'");
            return tables.Contains("transactions") && tables.Contains("accounts")
                ? null
                : "Это не резервная копия SaveMoney.";
        }
        catch (Exception)
        {
            return "Файл повреждён или это не база данных SQLite.";
        }
    }

    /// <summary>
    /// Заменяет базу содержимым резервной копии. Перед заменой текущая база
    /// сохраняется в <paramref name="safetyCopyPath"/> (если указан) — восстановление
    /// не должно быть необратимым. При ошибке копирования остаётся прежняя база.
    /// </summary>
    public void RestoreFrom(string backupPath, string? safetyCopyPath = null)
    {
        if (ValidateBackup(backupPath) is { } error)
        {
            throw new InvalidOperationException(error);
        }

        if (safetyCopyPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(safetyCopyPath)!);
            _db.Execute($"VACUUM INTO '{safetyCopyPath.Replace("'", "''")}'");
        }

        // Копируем во временный файл рядом: если копирование оборвётся,
        // рабочая база ещё не тронута.
        var staging = _path + ".restore";
        File.Copy(backupPath, staging, overwrite: true);

        _db.Close();
        _db.Dispose();
        try
        {
            File.Copy(staging, _path, overwrite: true);
            foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
            {
                if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
            }
        }
        finally
        {
            File.Delete(staging);
            _db = Open(_path);
        }

        DbMigrator.Migrate(_db);
        DefaultCategories.SeedIfNeeded(this);
    }

    /// <summary>Удаляет старые файлы в каталоге, оставляя <paramref name="keep"/> самых новых.</summary>
    public static int PruneOldFiles(string directory, string pattern, int keep)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        var stale = new DirectoryInfo(directory).GetFiles(pattern)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Skip(Math.Max(0, keep))
            .ToList();
        foreach (var file in stale)
        {
            try { file.Delete(); } catch (IOException) { /* занят шарингом — удалим в следующий раз */ }
        }

        return stale.Count;
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
