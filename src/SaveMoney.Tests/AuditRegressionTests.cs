using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.Tests;

public sealed class AuditRegressionTests : IDisposable
{
    private readonly TestDatabase test = new();
    public void Dispose() => test.Dispose();

    [Fact]
    public void IncomingDebtPayment_EditAndDelete_KeepDebtAndBalanceInSync()
    {
        var db = test.Db;
        var account = new Account { Name = "Cash" }; db.Accounts.Save(account);
        var debt = new Debt { PersonName = "Friend", AmountMinor = 10000, AccountId = account.Id };
        db.Debts.Save(debt);
        var transactions = new TransactionService(db);
        new DebtService(db, transactions).AddPayment(debt, 4000, 1000, true);
        var tx = Assert.Single(db.Transactions.GetRecent());
        Assert.Equal(tx.Id, Assert.Single(db.Debts.GetPayments(debt.Id)).TransactionId);
        tx.AmountMinor = 7000; transactions.Save(tx);
        Assert.Equal(3000, db.Debts.GetRemaining(debt));
        Assert.Equal(7000, new BalanceService(db).GetAccountBalance(account));
        new RepaymentService(db).Delete(tx);
        Assert.Equal(10000, db.Debts.GetRemaining(debt));
        Assert.Equal(0, new BalanceService(db).GetAccountBalance(account));
    }

    [Fact]
    public void IncomingDebtPayment_FailedTransactionLeavesDebtUnchanged()
    {
        var db = test.Db;
        var account = new Account { Name = "Cash" }; db.Accounts.Save(account);
        var debt = new Debt { PersonName = "Friend", AmountMinor = 10000, AccountId = account.Id }; db.Debts.Save(debt);
        db.Connection.Execute("CREATE TRIGGER fail_tx BEFORE INSERT ON transactions BEGIN SELECT RAISE(ABORT, 'failure'); END");
        Assert.ThrowsAny<Exception>(() => new DebtService(db, new TransactionService(db)).AddPayment(debt, 4000, 1000, true));
        Assert.Empty(db.Debts.GetPayments(debt.Id));
        Assert.Equal(10000, db.Debts.GetRemaining(debt));
    }

    [Fact]
    public void PendingRepayment_DoesNotChangeDebtUntilConfirmed()
    {
        var db = test.Db;
        var account = new Account { Name = "Cash" }; db.Accounts.Save(account);
        var debt = new Debt { PersonName = "Friend", AmountMinor = 10000, Direction = DebtDirection.IOwe }; db.Debts.Save(debt);
        var tx = new Transaction { Kind = TransactionKind.DebtRepayment, AccountId = account.Id, DebtId = debt.Id,
            AmountMinor = 4000, DateUnix = 1000, IsPendingReview = true };
        var service = new TransactionService(db); service.Save(tx);
        Assert.Equal(10000, db.Debts.GetRemaining(debt));
        tx.IsPendingReview = false; service.Save(tx);
        Assert.Equal(6000, db.Debts.GetRemaining(debt));
    }

    [Fact]
    public void Recurring_FailureLeavesDueDateAndWeeklyForecastIncludesEveryOccurrence()
    {
        var db = test.Db;
        var recurring = new RecurringPayment { AccountId = "account", AmountMinor = 1000,
            RecurrenceType = "weekly", NextDateUnix = 1_800_000_000 };
        db.RecurringPayments.Save(recurring);
        var service = new RecurringService(db, new TransactionService(db));
        Assert.Equal(4, service.GetUpcomingBetween(recurring.NextDateUnix, recurring.NextDateUnix + 28 * 86400).Count);
        db.Connection.Execute("CREATE TRIGGER fail_tx BEFORE INSERT ON transactions BEGIN SELECT RAISE(ABORT, 'failure'); END");
        Assert.Equal(0, service.MaterializeDue(recurring.NextDateUnix));
        Assert.Equal(recurring.NextDateUnix, db.RecurringPayments.Get(recurring.Id)!.NextDateUnix);
        Assert.Empty(db.Transactions.GetRecent());
    }

    [Fact]
    public void Recurring_FailedScheduleUpdateRollsBackTransaction()
    {
        var db = test.Db;
        var recurring = new RecurringPayment { AccountId = "account", AmountMinor = 1000,
            RecurrenceType = "weekly", NextDateUnix = 1_800_000_000 };
        db.RecurringPayments.Save(recurring);
        db.Connection.Execute("CREATE TRIGGER fail_schedule BEFORE UPDATE ON recurring_payments BEGIN SELECT RAISE(ABORT, 'failure'); END");
        var service = new RecurringService(db, new TransactionService(db));
        Assert.Equal(0, service.MaterializeDue(recurring.NextDateUnix));
        Assert.Equal(1_800_000_000, db.RecurringPayments.Get(recurring.Id)!.NextDateUnix);
        Assert.Empty(db.Transactions.GetRecent());
    }

    [Fact]
    public void DeletedChild_RemainsInBudgetAndReports()
    {
        var db = test.Db;
        var categories = new CategoryService(db);
        var root = categories.AddRoot("Root", CategoryKind.Expense, null);
        var child = categories.AddChild(root.Id, "Child", null);
        db.Budgets.Save(new Budget { CategoryId = root.Id, AmountMinor = 10000 });
        new TransactionService(db).Save(new Transaction { AccountId = "account", CategoryId = child.Id,
            AmountMinor = 5000, DateUnix = 1000, Kind = TransactionKind.Expense });
        db.Categories.SoftDelete(child);
        Assert.Equal(5000, Assert.Single(new BudgetService(db).GetStatuses(0, 2000)).FactMinor);
        Assert.Equal("Child", Assert.Single(new ReportService(db).GetExpenseByCategory(0, 2000)).Name);
    }
}
