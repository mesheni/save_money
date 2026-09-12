using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

public class BudgetAndDebtServiceTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    private static long Unix(int year, int month, int day) =>
        new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.FromHours(3)).ToUnixTimeSeconds();

    private void SaveExpense(string accountId, string categoryId, long amount, long date) =>
        _test.Db.Transactions.Save(new Transaction
        {
            AccountId = accountId,
            CategoryId = categoryId,
            AmountMinor = amount,
            Kind = TransactionKind.Expense,
            DateUnix = date,
        });

    [Fact]
    public void Budget_OnRootCategory_IncludesChildrenSpending()
    {
        var account = new Account { Name = "Карта" };
        _test.Db.Accounts.Save(account);

        var groceries = _test.Db.Categories.GetAllActive().First(c => c.Name == "Продукты");
        var magnet = _test.Db.Categories.GetAllActive().First(c => c.Name == "Магнит");

        _test.Db.Budgets.Save(new Budget { CategoryId = groceries.Id, AmountMinor = 20_000 });

        SaveExpense(account.Id, magnet.Id, 5_000, Unix(2026, 9, 2));
        SaveExpense(account.Id, groceries.Id, 3_000, Unix(2026, 9, 3));
        // вне периода и не та категория
        SaveExpense(account.Id, groceries.Id, 99_000, Unix(2026, 8, 2));
        SaveExpense(account.Id, magnet.Id, 50_000, Unix(2026, 10, 2));

        var rows = new BudgetService(_test.Db).GetStatuses(Unix(2026, 9, 1), Unix(2026, 10, 1));
        var row = Assert.Single(rows);

        Assert.Equal(8_000, row.FactMinor);
        Assert.False(row.IsOver);
    }

    [Fact]
    public void GeneralBudget_SumsAllExpenses_AndFlagsOver()
    {
        var account = new Account { Name = "Карта" };
        _test.Db.Accounts.Save(account);
        var cat = _test.Db.Categories.GetAllActive().First(c => c.Name == "Продукты");

        _test.Db.Budgets.Save(new Budget { CategoryId = null, AmountMinor = 10_000 });
        SaveExpense(account.Id, cat.Id, 6_000, Unix(2026, 9, 2));
        SaveExpense(account.Id, null, 7_000, Unix(2026, 9, 3));

        var row = Assert.Single(new BudgetService(_test.Db).GetStatuses(Unix(2026, 9, 1), Unix(2026, 10, 1)));
        Assert.True(row.IsGeneral);
        Assert.Equal(13_000, row.FactMinor);
        Assert.True(row.IsOver);
        Assert.Equal(100.0, row.Percent); // прогресс ограничен сверху
    }

    [Fact]
    public async Task DebtPayment_WithAccount_CreatesLinkedTransaction()
    {
        var transactions = new TransactionService(_test.Db);
        var service = new DebtService(_test.Db, transactions);

        var account = new Account { Name = "Сбер" };
        _test.Db.Accounts.Save(account);

        var owedToMe = new Debt
        {
            Direction = DebtDirection.OwedToMe,
            PersonName = "Аня",
            AmountMinor = 5_000,
            AccountId = account.Id,
        };
        _test.Db.Debts.Save(owedToMe);

        service.AddPayment(owedToMe, 5_000, Unix(2026, 9, 12), createTransaction: true);

        // Долг закрылся, транзакция-доход создана в категорию «Возврат долга»
        var debt = _test.Db.Debts.Get(owedToMe.Id);
        Assert.NotNull(debt);
        Assert.Equal(DebtStatus.Settled, debt.Status);

        var tx = Assert.Single(_test.Db.Transactions.GetRecent());
        Assert.Equal(TransactionKind.Income, tx.Kind);
        Assert.Equal(account.Id, tx.AccountId);
        Assert.Equal("Аня", tx.Payee);
        var category = _test.Db.Categories.Get(tx.CategoryId!);
        Assert.NotNull(category);
        Assert.Equal("Возврат долга", category.Name);
    }

    [Fact]
    public async Task DebtPayment_IAmOwe_CreatesExpenseTransaction()
    {
        var transactions = new TransactionService(_test.Db);
        var service = new DebtService(_test.Db, transactions);

        var account = new Account { Name = "Сбер" };
        _test.Db.Accounts.Save(account);

        var iOwe = new Debt
        {
            Direction = DebtDirection.IOwe,
            PersonName = "Банк",
            AmountMinor = 10_000,
            AccountId = account.Id,
        };
        _test.Db.Debts.Save(iOwe);

        service.AddPayment(iOwe, 4_000, Unix(2026, 9, 12), createTransaction: true);

        var tx = Assert.Single(_test.Db.Transactions.GetRecent());
        Assert.Equal(TransactionKind.Expense, tx.Kind);
        Assert.Equal(4_000, tx.AmountMinor);

        var debt = _test.Db.Debts.Get(iOwe.Id);
        Assert.NotNull(debt);
        Assert.Equal(DebtStatus.Active, debt.Status);
        Assert.Equal(6_000, _test.Db.Debts.GetRemaining(debt));
    }

    [Fact]
    public void DebtPayment_WithoutAccount_NoTransaction()
    {
        var transactions = new TransactionService(_test.Db);
        var service = new DebtService(_test.Db, transactions);

        var debt = new Debt { Direction = DebtDirection.OwedToMe, PersonName = "Кто-то", AmountMinor = 100 };
        _test.Db.Debts.Save(debt);

        service.AddPayment(debt, 50, Unix(2026, 9, 12), createTransaction: true);

        Assert.Equal(0, _test.Db.Transactions.CountAll());
    }
}
