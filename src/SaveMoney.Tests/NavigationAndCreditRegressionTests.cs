using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.Tests;

public sealed class NavigationAndCreditRegressionTests
{
    [Theory]
    [InlineData(-64, 0, 1)]
    [InlineData(64, 32, -1)]
    [InlineData(63, 0, 0)]
    [InlineData(-100, 51, 0)]
    [InlineData(0, 100, 0)]
    public void SwipeRequiresDistanceAndHorizontalIntent(double dx, double dy, int expected) =>
        Assert.Equal(expected, TabSwipePolicy.GetStep(dx, dy));

    [Theory]
    [InlineData(AccountKind.Credit, -5000, false, false, true)]
    [InlineData(AccountKind.Credit, 0, false, false, false)]
    [InlineData(AccountKind.Credit, 5000, false, false, false)]
    [InlineData(AccountKind.Cash, -5000, false, false, false)]
    [InlineData(AccountKind.Credit, -5000, true, false, false)]
    [InlineData(AccountKind.Credit, -5000, false, true, false)]
    public void OnlyAvailableNegativeCreditAccountsAreDebts(string kind, long balance, bool archived, bool deleted, bool expected)
    {
        using var test = new TestDatabase();
        var account = new Account { Name = "Счёт", Kind = kind, InitialBalanceMinor = balance,
            Archived = archived, IsDeleted = deleted };
        test.Db.Accounts.Save(account);
        var targets = new RepaymentService(test.Db).GetTargets();
        Assert.Equal(expected, targets.Any(t => t.Id == account.Id));
        if (expected) Assert.Equal(5000, Assert.Single(targets.Where(t => t.Id == account.Id)).RemainingMinor);
    }

    [Fact]
    public void CreditDebtUsesActualBalanceAndCombinesWithPersonalDebt()
    {
        using var test = new TestDatabase();
        var db = test.Db;
        var source = new Account { Name = "Оплата", InitialBalanceMinor = 20000 };
        var credit = new Account { Name = "Кредитка", Kind = AccountKind.Credit, InitialBalanceMinor = 0 };
        db.Accounts.Save(source); db.Accounts.Save(credit);
        db.Transactions.Save(new Transaction { Kind = TransactionKind.Expense, AccountId = credit.Id, AmountMinor = 10000 });
        db.Debts.Save(new Debt { PersonName = "Друг", Direction = DebtDirection.IOwe, AmountMinor = 3000 });
        var service = new RepaymentService(db);
        Assert.Equal(13000, service.GetTargets().Sum(t => t.RemainingMinor));
        var payment = new Transaction { Kind = TransactionKind.DebtRepayment, AccountId = source.Id,
            TransferAccountId = credit.Id, AmountMinor = 4000 };
        service.Save(payment);
        Assert.Equal(9000, service.GetTargets().Sum(t => t.RemainingMinor));
        payment.AmountMinor = 10000;
        service.Save(payment);
        Assert.DoesNotContain(service.GetTargets(), t => t.Id == credit.Id);
        Assert.Equal(3000, service.GetTargets().Sum(t => t.RemainingMinor));
    }
}
