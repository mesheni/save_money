using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

/// <summary>Регрессии по ревью 2026-11: удалённый счёт, статус долга, динамика «Всё».</summary>
public class ReviewFixesRegressionTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    private static long Unix(int year, int month, int day) =>
        new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.FromHours(3)).ToUnixTimeSeconds();

    private Account AddAccount(string name = "Карта")
    {
        var account = new Account { Name = name, Kind = AccountKind.Card };
        _test.Db.Accounts.Save(account);
        return account;
    }

    private RecurringService Recurring() => new(_test.Db, new TransactionService(_test.Db));

    // --- 1. Регулярные платежи и удалённый счёт ---

    [Fact]
    public void DeletingAccount_DeactivatesItsRecurringPayments()
    {
        var doomed = AddAccount("Старая карта");
        var kept = AddAccount("Наличные");
        var onDoomed = new RecurringPayment { AccountId = doomed.Id, AmountMinor = 100, NextDateUnix = Unix(2026, 1, 1) };
        var onKept = new RecurringPayment { AccountId = kept.Id, AmountMinor = 100, NextDateUnix = Unix(2026, 1, 1) };
        _test.Db.RecurringPayments.Save(onDoomed);
        _test.Db.RecurringPayments.Save(onKept);

        _test.Db.Accounts.SoftDelete(doomed);

        Assert.False(_test.Db.RecurringPayments.Get(onDoomed.Id)!.IsActive);
        Assert.True(_test.Db.RecurringPayments.Get(onKept.Id)!.IsActive);
    }

    [Fact]
    public void MaterializeDue_SkipsPaymentsOfDeletedAccount()
    {
        var account = AddAccount();
        var recurring = new RecurringPayment { AccountId = account.Id, AmountMinor = 100, NextDateUnix = Unix(2026, 1, 1) };
        _test.Db.RecurringPayments.Save(recurring);

        // Старая база: счёт удалён до исправления, платёж остался активным.
        account.IsDeleted = true;
        _test.Db.Connection.Update(account);

        var created = Recurring().MaterializeDue(Unix(2026, 2, 1));

        Assert.Equal(0, created);
        Assert.Equal(0, _test.Db.Transactions.CountAll());
    }

    // --- 5. Статус долга при правке суммы ---

    private Debt AddDebtWithPayment(long amount, long paid)
    {
        var debt = new Debt { PersonName = "Иван", AmountMinor = amount, Direction = DebtDirection.IOwe };
        _test.Db.Debts.Save(debt);
        _test.Db.Debts.AddPayment(new DebtPayment { DebtId = debt.Id, AmountMinor = paid, DateUnix = Unix(2026, 1, 1) });
        return _test.Db.Debts.Get(debt.Id)!;
    }

    [Fact]
    public void EditDebt_AmountBelowPaid_SettlesDebt()
    {
        var debt = AddDebtWithPayment(10_000, 4_000);
        Assert.Equal(DebtStatus.Active, debt.Status);

        debt.AmountMinor = 3_000;
        _test.Db.Debts.SaveEdited(debt);

        var stored = _test.Db.Debts.Get(debt.Id)!;
        Assert.Equal(DebtStatus.Settled, stored.Status);
        Assert.NotNull(stored.SettledDateUnix);
    }

    [Fact]
    public void EditDebt_RaisingSettledAmount_ReopensDebt()
    {
        var debt = AddDebtWithPayment(5_000, 5_000);
        Assert.Equal(DebtStatus.Settled, debt.Status);

        debt.AmountMinor = 8_000;
        _test.Db.Debts.SaveEdited(debt);

        var stored = _test.Db.Debts.Get(debt.Id)!;
        Assert.Equal(DebtStatus.Active, stored.Status);
        Assert.Null(stored.SettledDateUnix);
    }

    [Fact]
    public void EditDebt_SameAmount_KeepsManualSettle()
    {
        var debt = AddDebtWithPayment(10_000, 1_000);
        debt.Status = DebtStatus.Settled; // «простили» остаток вручную
        _test.Db.Debts.Save(debt);

        debt.Note = "простили";
        _test.Db.Debts.SaveEdited(debt);

        Assert.Equal(DebtStatus.Settled, _test.Db.Debts.Get(debt.Id)!.Status);
    }

    // --- 6. Динамика за длинный период ---

    [Fact]
    public void Dynamics_MultiYear_UsesMonthlyBuckets()
    {
        var points = new ReportService(_test.Db).GetDynamics(Unix(2024, 3, 15), Unix(2026, 3, 1));

        // 15.03.2024 … 28.02.2026: первый неполный месяц + 23 полных.
        Assert.Equal(24, points.Count);
        Assert.Equal("03.2024", points[0].Label);
        Assert.Equal("02.2026", points[^1].Label);
        Assert.Equal(DynamicsGranularity.Month, ReportService.GetGranularity(Unix(2024, 3, 15), Unix(2026, 3, 1)));
    }

    [Fact]
    public void Dynamics_AcrossNewYear_LabelsCarryYear()
    {
        var points = new ReportService(_test.Db).GetDynamics(Unix(2025, 12, 30), Unix(2026, 1, 2));

        Assert.Equal(["30.12.25", "31.12.25", "01.01.26"], points.Select(p => p.Label));
    }

    [Fact]
    public void EarliestDate_IgnoresDeletedAndDrafts()
    {
        var account = AddAccount();
        Assert.Null(_test.Db.Transactions.GetEarliestDateUnix());

        var deleted = new Transaction { AccountId = account.Id, AmountMinor = 1, DateUnix = Unix(2020, 1, 1) };
        _test.Db.Transactions.Save(deleted);
        _test.Db.Transactions.SoftDelete(deleted);
        _test.Db.Transactions.Save(new Transaction { AccountId = account.Id, AmountMinor = 1, DateUnix = Unix(2021, 1, 1), IsPendingReview = true });
        _test.Db.Transactions.Save(new Transaction { AccountId = account.Id, AmountMinor = 1, DateUnix = Unix(2025, 5, 5) });

        Assert.Equal(Unix(2025, 5, 5), _test.Db.Transactions.GetEarliestDateUnix());
    }
}
