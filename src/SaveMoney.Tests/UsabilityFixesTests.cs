using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

/// <summary>Удобство по ревью 2026-11: пропуски регулярных, отчёты, долги, удаление счёта, восстановление.</summary>
public class UsabilityFixesTests : IDisposable
{
    private readonly TestDatabase _test = new();
    private readonly List<string> _files = [];

    public void Dispose()
    {
        _test.Dispose();
        foreach (var file in _files.Where(File.Exists)) File.Delete(file);
    }

    private static long Unix(DateTime local) =>
        new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();

    private Account AddAccount(string name = "Карта", long initial = 0)
    {
        var account = new Account { Name = name, Kind = AccountKind.Card, InitialBalanceMinor = initial };
        _test.Db.Accounts.Save(account);
        return account;
    }

    private string TempPath(string suffix = ".db")
    {
        var path = Path.Combine(Path.GetTempPath(), $"savemoney-usability-{Guid.NewGuid():N}{suffix}");
        _files.Add(path);
        return path;
    }

    // --- Пропущенные сроки регулярных платежей ---

    [Fact]
    public void MaterializeDue_CreatesOneTransactionPerMissedPeriod()
    {
        var account = AddAccount();
        var first = new DateTime(2026, 1, 1, 12, 0, 0);
        _test.Db.RecurringPayments.Save(new RecurringPayment
        {
            AccountId = account.Id, AmountMinor = 1_000, RecurrenceType = "weekly", NextDateUnix = Unix(first),
        });

        // Четыре недели без открытия приложения: 1, 8, 15, 22 января.
        var created = new RecurringService(_test.Db, new TransactionService(_test.Db))
            .MaterializeDue(Unix(new DateTime(2026, 1, 25)));

        Assert.Equal(4, created);
        var dates = _test.Db.Transactions.GetRecent()
            .Select(t => DateTimeOffset.FromUnixTimeSeconds(t.DateUnix).LocalDateTime.Day)
            .OrderBy(d => d);
        Assert.Equal([1, 8, 15, 22], dates);
        Assert.Equal(Unix(new DateTime(2026, 1, 29, 12, 0, 0)), _test.Db.RecurringPayments.GetAllActive().Single().NextDateUnix);
    }

    [Fact]
    public void MaterializeDue_CapsCatchUpAndStillMovesDateForward()
    {
        var account = AddAccount();
        var now = Unix(new DateTime(2026, 1, 1));
        _test.Db.RecurringPayments.Save(new RecurringPayment
        {
            AccountId = account.Id, AmountMinor = 100, RecurrenceType = "every_n_days", IntervalDays = 1,
            NextDateUnix = now - 400L * 86400,
        });

        var created = new RecurringService(_test.Db, new TransactionService(_test.Db)).MaterializeDue(now);

        Assert.Equal(RecurringService.MaxCatchUpPerPayment, created);
        Assert.True(_test.Db.RecurringPayments.GetAllActive().Single().NextDateUnix > now);
    }

    // --- Отчёты ---

    [Fact]
    public void Report_RollsSubcategoriesIntoRoot()
    {
        var categories = new CategoryService(_test.Db);
        var food = categories.AddRoot("Еда", CategoryKind.Expense, null);
        var cafe = categories.AddChild(food.Id, "Кафе", null);
        var account = AddAccount();
        _test.Db.Transactions.Save(new Transaction { AccountId = account.Id, CategoryId = food.Id, AmountMinor = 300, DateUnix = 10 });
        _test.Db.Transactions.Save(new Transaction { AccountId = account.Id, CategoryId = cafe.Id, AmountMinor = 700, DateUnix = 20 });

        var slice = Assert.Single(new ReportService(_test.Db).GetExpenseByCategory(0, 100));
        Assert.Equal("Еда", slice.Name);
        Assert.Equal(1_000, slice.AmountMinor);
    }

    [Fact]
    public void ReportPeriods_AreWholeDays_EndingTonight()
    {
        var now = new DateTime(2026, 11, 23, 15, 40, 0);

        var week = ReportPeriods.LastDays(now, 7);
        Assert.Equal(new DateTime(2026, 11, 17), week.From);
        Assert.Equal(new DateTime(2026, 11, 24), week.To);

        var month = ReportPeriods.LastMonths(now, 1);
        Assert.Equal(new DateTime(2026, 10, 24), month.From);
        Assert.Equal(new DateTime(2026, 11, 24), month.To);
    }

    // --- Долги ---

    [Fact]
    public void DeletingRepayment_KeepsManuallySettledDebtClosed()
    {
        var account = AddAccount(initial: 100_000);
        var debt = new Debt { PersonName = "Банк", AmountMinor = 10_000, Direction = DebtDirection.IOwe };
        _test.Db.Debts.Save(debt);
        var repayment = new Transaction
        {
            AccountId = account.Id, DebtId = debt.Id, AmountMinor = 2_000,
            Kind = TransactionKind.DebtRepayment, DateUnix = 100,
        };
        new TransactionService(_test.Db).Save(repayment);

        // Остаток 8 000 «простили» вручную.
        debt = _test.Db.Debts.Get(debt.Id)!;
        debt.Status = DebtStatus.Settled;
        _test.Db.Debts.Save(debt);

        new RepaymentService(_test.Db).Delete(repayment);

        Assert.Equal(DebtStatus.Settled, _test.Db.Debts.Get(debt.Id)!.Status);
    }

    // --- Удаление счёта ---

    [Fact]
    public void AccountDeletionImpact_MentionsBalanceRecurringAndDebts()
    {
        var account = AddAccount("Сбер", initial: 150_000);
        _test.Db.RecurringPayments.Save(new RecurringPayment { AccountId = account.Id, AmountMinor = 1, NextDateUnix = 1 });
        _test.Db.Debts.Save(new Debt { PersonName = "Иван", AmountMinor = 1, AccountId = account.Id });

        var text = AccountDeletion.DescribeImpact(_test.Db, account);

        Assert.Contains(MoneyFormat.Rubles(150_000), text);
        Assert.Contains("Регулярные платежи", text);
        Assert.Contains("долги", text);
    }

    [Fact]
    public void AccountDeletionImpact_EmptyAccount_IsShort()
    {
        var text = AccountDeletion.DescribeImpact(_test.Db, AddAccount("Пустой"));

        Assert.DoesNotContain("\n", text);
    }

    // --- Резервная копия: восстановление и очистка ---

    [Fact]
    public void RestoreFrom_ReplacesData_AndKeepsSafetyCopy()
    {
        AddAccount("Из копии");
        var backup = TempPath();
        _test.Db.Connection.Execute($"VACUUM INTO '{backup}'");

        AddAccount("После копии");
        var safety = TempPath();

        _test.Db.RestoreFrom(backup, safety);

        Assert.Equal(["Из копии"], _test.Db.Accounts.GetAll().Select(a => a.Name));
        using var saved = new AppDatabase(safety);
        Assert.Equal(2, saved.Accounts.GetAll().Count);
    }

    [Fact]
    public void RestoreFrom_RejectsForeignFile_AndKeepsCurrentData()
    {
        AddAccount("Текущий");
        var junk = TempPath();
        File.WriteAllText(junk, "not a database");

        Assert.NotNull(AppDatabase.ValidateBackup(junk));
        Assert.Throws<InvalidOperationException>(() => _test.Db.RestoreFrom(junk));
        Assert.Equal("Текущий", Assert.Single(_test.Db.Accounts.GetAll()).Name);
    }

    [Fact]
    public void PruneOldFiles_KeepsNewest()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"savemoney-prune-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            for (var i = 0; i < 7; i++)
            {
                var file = Path.Combine(dir, $"savemoney-backup-{i}.db");
                File.WriteAllText(file, "x");
                File.SetLastWriteTimeUtc(file, new DateTime(2026, 1, 1).AddDays(i));
            }

            Assert.Equal(2, AppDatabase.PruneOldFiles(dir, "savemoney-backup-*.db", 5));
            Assert.False(File.Exists(Path.Combine(dir, "savemoney-backup-0.db")));
            Assert.True(File.Exists(Path.Combine(dir, "savemoney-backup-6.db")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
