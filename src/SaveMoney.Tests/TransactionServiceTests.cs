using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

public class TransactionServiceTests : IDisposable
{
    private readonly TestDatabase _test = new();
    private readonly TransactionService _service;

    public TransactionServiceTests()
    {
        _service = new TransactionService(_test.Db);
    }

    public void Dispose() => _test.Dispose();

    private Category FindCategory(string name) =>
        _test.Db.Categories.GetAllActive().First(c => c.Name == name);

    [Fact]
    public void Save_LearnsMerchantRule_AndGuessesNextTime()
    {
        var groceries = FindCategory("Продукты");
        var account = new Account { Name = "Карта" };
        _test.Db.Accounts.Save(account);

        _service.Save(new Transaction
        {
            AccountId = account.Id,
            CategoryId = groceries.Id,
            AmountMinor = 50_000,
            Kind = TransactionKind.Expense,
            Payee = "Пятёрочка",
            DateUnix = 1_000,
        });

        var guess = _service.GuessCategoryId("ПЯТЁРОЧКА на Ленина");
        Assert.Equal(groceries.Id, guess);
    }

    public static IEnumerable<object[]> InvalidTransactions =>
    [
        [new Func<Transaction>(() => new Transaction { AmountMinor = 0, Kind = TransactionKind.Expense })],
        [new Func<Transaction>(() => new Transaction { AmountMinor = 100, Kind = TransactionKind.Transfer })],
        [new Func<Transaction>(() => new Transaction
        {
            AmountMinor = 100,
            Kind = TransactionKind.Transfer,
            AccountId = "a",
            TransferAccountId = "a",
        })],
    ];

    [Theory]
    [MemberData(nameof(InvalidTransactions))]
    public void Save_RejectsInvalid(Func<Transaction> factory) =>
        Assert.ThrowsAny<ArgumentException>(() => _service.Save(factory()));

    [Fact]
    public void Save_Transfer_ClearsCategoryAndPayee()
    {
        var account = new Account { Name = "А" };
        var target = new Account { Name = "Б" };
        _test.Db.Accounts.Save(account);
        _test.Db.Accounts.Save(target);

        var tx = _service.Save(new Transaction
        {
            AccountId = account.Id,
            TransferAccountId = target.Id,
            AmountMinor = 10_000,
            Kind = TransactionKind.Transfer,
            CategoryId = FindCategory("Продукты").Id,
            Payee = "Кто-то",
        });

        Assert.Null(tx.CategoryId);
        Assert.Null(tx.Payee);
        Assert.Null(_service.GuessCategoryId("Кто-то"));
    }

    [Fact]
    public void Save_RemembersLastCategoryPerKind()
    {
        var expense = FindCategory("Продукты");
        var income = FindCategory("Зарплата");

        _service.Save(new Transaction { AccountId = "a", CategoryId = expense.Id, AmountMinor = 1, Kind = TransactionKind.Expense, DateUnix = 1 });
        _service.Save(new Transaction { AccountId = "a", CategoryId = income.Id, AmountMinor = 1, Kind = TransactionKind.Income, DateUnix = 2 });

        Assert.Equal(expense.Id, _service.GetLastCategoryId(TransactionKind.Expense));
        Assert.Equal(income.Id, _service.GetLastCategoryId(TransactionKind.Income));
    }

    [Fact]
    public void Save_RepeatedPayee_UpdatesExistingRule()
    {
        var groceries = FindCategory("Продукты");
        var cafe = FindCategory("Кафе и рестораны");

        _service.Save(new Transaction { AccountId = "a", CategoryId = cafe.Id, AmountMinor = 1, Kind = TransactionKind.Expense, Payee = "магнит", DateUnix = 1 });
        _service.Save(new Transaction { AccountId = "a", CategoryId = groceries.Id, AmountMinor = 1, Kind = TransactionKind.Expense, Payee = "Магнит", DateUnix = 2 });

        var rules = _test.Db.MerchantRules.GetAllActive().Where(r => r.Pattern.StartsWith("магнит", StringComparison.OrdinalIgnoreCase)).ToList();
        var rule = Assert.Single(rules);
        Assert.Equal(groceries.Id, rule.CategoryId);
        Assert.Equal(2, rule.UseCount);
    }
}
