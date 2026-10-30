using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

public sealed class RepaymentServiceTests : IDisposable
{
    private readonly TestDatabase test = new();
    private AppDatabase Db => test.Db;
    private Account Source()
    {
        var account = new Account { Name = "Оплата", InitialBalanceMinor = 100_000 };
        Db.Accounts.Save(account);
        return account;
    }
    private Debt Debt(string kind = DebtKind.Personal)
    {
        var debt = new Debt { PersonName = "Кредитор", Direction = DebtDirection.IOwe, Kind = kind, AmountMinor = 10_000 };
        Db.Debts.Save(debt);
        return debt;
    }
    private Transaction Pay(Account source, Debt debt, long amount) => new TransactionService(Db).Save(new Transaction
    {
        Kind = TransactionKind.DebtRepayment, AccountId = source.Id, DebtId = debt.Id,
        AmountMinor = amount, DateUnix = 1000,
    });
    public void Dispose() => test.Dispose();

    [Theory]
    [InlineData(DebtKind.Personal)]
    [InlineData(DebtKind.ConsumerLoan)]
    public void Principal_PartialFullEditDelete_KeepBalancesAndPaymentsConsistent(string kind)
    {
        var source = Source(); var debt = Debt(kind);
        var tx = Pay(source, debt, 4000);
        Assert.Equal(6000, Db.Debts.GetRemaining(debt));
        Assert.Equal(96_000, new BalanceService(Db).GetAccountBalance(source));
        Assert.Equal(tx.Id, Assert.Single(Db.Debts.GetPayments(debt.Id)).TransactionId);
        tx.AmountMinor = 10_000;
        new TransactionService(Db).Save(tx);
        Assert.Equal(DebtStatus.Settled, Db.Debts.Get(debt.Id)!.Status);
        Assert.Single(Db.Debts.GetPayments(debt.Id));
        Assert.Contains(new RepaymentService(Db).GetTargets(tx.Id), t => t.Id == debt.Id && t.RemainingMinor == 10_000);
        tx.AmountMinor = 3000;
        new TransactionService(Db).Save(tx);
        Assert.Equal(DebtStatus.Active, Db.Debts.Get(debt.Id)!.Status);
        Assert.Equal(7000, Db.Debts.GetRemaining(debt));
        new RepaymentService(Db).Delete(tx);
        Assert.Empty(Db.Debts.GetPayments(debt.Id));
        Assert.Equal(10_000, Db.Debts.GetRemaining(debt));
        Assert.Equal(100_000, new BalanceService(Db).GetAccountBalance(source));
    }

    [Fact]
    public void Credit_PartialFullEditDelete_AdjustBothAccounts_AndTotal()
    {
        var source = Source();
        var credit = new Account { Name = "Кредитка", Kind = AccountKind.Credit, InitialBalanceMinor = -10_000 };
        Db.Accounts.Save(credit);
        var tx = new Transaction { Kind = TransactionKind.DebtRepayment, AccountId = source.Id,
            TransferAccountId = credit.Id, AmountMinor = 4000, DateUnix = 1000 };
        var service = new TransactionService(Db); var balance = new BalanceService(Db);
        service.Save(tx);
        Assert.Equal(96_000, balance.GetAccountBalance(source));
        Assert.Equal(-6000, balance.GetAccountBalance(credit));
        Assert.Equal(90_000, balance.GetTotalBalance());
        tx.AmountMinor = 10_000; service.Save(tx);
        Assert.Equal(0, balance.GetAccountBalance(credit));
        Assert.DoesNotContain(new RepaymentService(Db).GetTargets(), t => t.Id == credit.Id);
        tx.AmountMinor = 2000; service.Save(tx);
        Assert.Equal(-8000, balance.GetAccountBalance(credit));
        new RepaymentService(Db).Delete(tx);
        Assert.Equal(100_000, balance.GetAccountBalance(source));
        Assert.Equal(-10_000, balance.GetAccountBalance(credit));
    }

    [Fact]
    public void Repayment_IsVisibleInHistoryAndCsv_ButExcludedFromReportsAndBudget()
    {
        var tx = Pay(Source(), Debt(), 5000);
        Db.Budgets.Save(new Budget { AmountMinor = 20_000 });
        Assert.Equal((0L, 0L), new ReportService(Db).GetTotals(0, 2000));
        Assert.Empty(new ReportService(Db).GetByCategory(0, 2000));
        Assert.Equal(0, Assert.Single(new BudgetService(Db).GetStatuses(0, 2000)).FactMinor);
        var group = Assert.Single(new HistoryService(Db).GetGroups(0, 2000));
        Assert.Equal(0, group.ExpenseMinor);
        var item = Assert.Single(group.Items);
        Assert.Equal(-5000, item.SignedAmountMinor);
        Assert.Contains("Кредитор", item.Title);
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var csv = File.ReadAllText(new CsvExportService(Db).ExportTransactions(0, 2000, dir));
            Assert.Contains("Погашение долга", csv); Assert.Contains("Кредитор", csv);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void OverpaymentAndFailedEdit_RollBackOriginalPayment()
    {
        var source = Source(); var debt = Debt();
        var tx = Pay(source, debt, 4000);
        tx.AmountMinor = 10_001;
        Assert.Throws<ArgumentException>(() => new TransactionService(Db).Save(tx));
        Assert.Equal(4000, Db.Transactions.Get(tx.Id)!.AmountMinor);
        Assert.Equal(6000, Db.Debts.GetRemaining(debt));
        Assert.Single(Db.Debts.GetPayments(debt.Id));
        Assert.Throws<ArgumentException>(() => Pay(source, debt, 6001));
        Assert.Single(Db.Transactions.GetRecent());
    }

    [Fact]
    public void DatabaseFailure_RollsBackDebtStatusPaymentAndOperation()
    {
        var source = Source(); var debt = Debt();
        Db.Connection.Execute("CREATE TRIGGER reject_repayment BEFORE INSERT ON transactions BEGIN SELECT RAISE(ABORT, 'test failure'); END");
        Assert.ThrowsAny<Exception>(() => Pay(source, debt, 10_000));
        Assert.Empty(Db.Debts.GetPayments(debt.Id));
        Assert.Empty(Db.Transactions.GetRecent());
        Assert.Equal(DebtStatus.Active, Db.Debts.Get(debt.Id)!.Status);
        Assert.Null(Db.Settings.Get(RepaymentService.LastAccountKey));
    }

    [Fact]
    public void ChangingTarget_AndChangingToExpense_UndoOldDebtPayment()
    {
        var source = Source(); var first = Debt(); var second = Debt(DebtKind.ConsumerLoan);
        var tx = Pay(source, first, 10_000);
        tx.DebtId = second.Id; new TransactionService(Db).Save(tx);
        Assert.Equal(10_000, Db.Debts.GetRemaining(first));
        Assert.Equal(DebtStatus.Active, Db.Debts.Get(first.Id)!.Status);
        Assert.Equal(0, Db.Debts.GetRemaining(second));
        tx.Kind = TransactionKind.Expense; new TransactionService(Db).Save(tx);
        Assert.Equal(10_000, Db.Debts.GetRemaining(second));
        Assert.Null(Db.Transactions.Get(tx.Id)!.DebtId);
    }

    [Fact]
    public void Targets_ExcludeClosedReceivablesArchivedAndNonCreditAccounts()
    {
        var debt = Debt(); var receivable = Debt(); receivable.Direction = DebtDirection.OwedToMe; Db.Debts.Save(receivable);
        var closed = Debt(); closed.Status = DebtStatus.Settled; Db.Debts.Save(closed);
        Db.Accounts.Save(new Account { Name = "Обычная карта", InitialBalanceMinor = -1000 });
        Db.Accounts.Save(new Account { Name = "Архив", Kind = AccountKind.Credit, Archived = true, InitialBalanceMinor = -1000 });
        Assert.Equal(debt.Id, Assert.Single(new RepaymentService(Db).GetTargets()).Id);
    }

    [Fact]
    public void Validation_RejectsZeroMissingSourceAndSelfPayment()
    {
        var source = Source(); var debt = Debt();
        Assert.Throws<ArgumentException>(() => Pay(source, debt, 0));
        Db.Accounts.SoftDelete(source);
        Assert.Throws<ArgumentException>(() => Pay(source, debt, 100));
        var credit = new Account { Name = "Кредитка", Kind = AccountKind.Credit, InitialBalanceMinor = -1000 }; Db.Accounts.Save(credit);
        Assert.Throws<ArgumentException>(() => new TransactionService(Db).Save(new Transaction
        { AccountId = credit.Id, TransferAccountId = credit.Id, Kind = TransactionKind.DebtRepayment, AmountMinor = 100 }));
        Assert.Empty(Db.Transactions.GetRecent());
    }

    [Fact]
    public void MigrationFromV2_PreservesOldAmountsAndDoesNotInventLinks()
    {
        var source = Source(); var debt = Debt();
        var old = new Transaction { AccountId = source.Id, Kind = TransactionKind.Transfer,
            TransferAccountId = "old-target", AmountMinor = 3456, DateUnix = 1000 };
        Db.Transactions.Save(old);
        Db.Debts.AddPayment(new DebtPayment { DebtId = debt.Id, AmountMinor = 1234, DateUnix = 1000 });
        Db.Connection.Execute("DROP INDEX IF EXISTS transactions_debt_id");
        Db.Connection.Execute("DROP INDEX IF EXISTS debt_payments_transaction_id");
        Db.Connection.Execute("ALTER TABLE transactions DROP COLUMN debt_id");
        Db.Connection.Execute("ALTER TABLE debt_payments DROP COLUMN transaction_id");
        Db.Connection.Execute("ALTER TABLE debts DROP COLUMN kind");
        Db.Connection.Execute("PRAGMA user_version = 2");
        DbMigrator.Migrate(Db.Connection);
        Assert.Equal(3, Db.Connection.ExecuteScalar<int>("PRAGMA user_version"));
        Assert.Equal(DebtKind.Personal, Db.Debts.Get(debt.Id)!.Kind);
        Assert.Equal(8766, Db.Debts.GetRemaining(debt));
        Assert.Null(Assert.Single(Db.Debts.GetPayments(debt.Id)).TransactionId);
        var migrated = Db.Transactions.Get(old.Id)!;
        Assert.Null(migrated.DebtId);
        Assert.Equal(3456, migrated.AmountMinor);
        Assert.Equal(TransactionKind.Transfer, migrated.Kind);
        Assert.Equal(100_000, Db.Accounts.Get(source.Id)!.InitialBalanceMinor);
        DbMigrator.Migrate(Db.Connection);
        Assert.Single(Db.Debts.GetPayments(debt.Id));
    }
}
