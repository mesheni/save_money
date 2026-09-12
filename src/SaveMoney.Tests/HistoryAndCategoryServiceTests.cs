using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

public class HistoryAndCategoryServiceTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    private static long Unix(int year, int month, int day, int hour = 12) =>
        new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.FromHours(3)).ToUnixTimeSeconds();

    [Fact]
    public void GetGroups_GroupsByLocalDay_AndComputesSums()
    {
        var account = new Account { Name = "Карта" };
        _test.Db.Accounts.Save(account);
        var groceries = _test.Db.Categories.GetAllActive().First(c => c.Name == "Продукты");
        var salary = _test.Db.Categories.GetAllActive().First(c => c.Name == "Зарплата");

        SaveTx(account.Id, TransactionKind.Expense, 10_000, groceries.Id, Unix(2026, 9, 10, 9));
        SaveTx(account.Id, TransactionKind.Expense, 5_000, groceries.Id, Unix(2026, 9, 10, 20));
        SaveTx(account.Id, TransactionKind.Income, 100_000, salary.Id, Unix(2026, 9, 10, 21));
        SaveTx(account.Id, TransactionKind.Expense, 7_000, groceries.Id, Unix(2026, 9, 11, 10));

        var groups = new HistoryService(_test.Db).GetGroups(Unix(2026, 9, 1), Unix(2026, 9, 30));

        Assert.Equal(2, groups.Count);

        var day11 = groups.Single(g => g.Date == new DateTime(2026, 9, 11));
        Assert.Equal(7_000, day11.ExpenseMinor);

        var day10 = groups.Single(g => g.Date == new DateTime(2026, 9, 10));
        Assert.Equal(15_000, day10.ExpenseMinor);
        Assert.Equal(100_000, day10.IncomeMinor);
        Assert.Equal(3, day10.Items.Count);
    }

    [Fact]
    public void GetGroups_AccountFilter_IncludesBothTransferSides()
    {
        var from = new Account { Name = "А" };
        var to = new Account { Name = "Б" };
        var other = new Account { Name = "В" };
        _test.Db.Accounts.Save(from);
        _test.Db.Accounts.Save(to);
        _test.Db.Accounts.Save(other);

        SaveTx(from.Id, TransactionKind.Transfer, 12_000, null, Unix(2026, 9, 10), transferTo: to.Id);
        SaveTx(other.Id, TransactionKind.Expense, 3_000, null, Unix(2026, 9, 10));

        var service = new HistoryService(_test.Db);

        Assert.Single(service.GetGroups(Unix(2026, 9, 1), Unix(2026, 9, 30), from.Id).Single().Items);
        Assert.Single(service.GetGroups(Unix(2026, 9, 1), Unix(2026, 9, 30), to.Id).Single().Items);
        Assert.Empty(service.GetGroups(Unix(2026, 9, 1), Unix(2026, 9, 30), other.Id).Single().Items
            .Where(i => i.Kind == TransactionKind.Transfer));
    }

    [Fact]
    public void HistoryItem_ExpenseIsNegative()
    {
        var account = new Account { Name = "А" };
        _test.Db.Accounts.Save(account);
        SaveTx(account.Id, TransactionKind.Expense, 5_000, null, Unix(2026, 9, 10));

        var item = new HistoryService(_test.Db).GetGroups(Unix(2026, 9, 1), Unix(2026, 9, 30)).Single().Items.Single();
        Assert.Equal(-5_000, item.SignedAmountMinor);
        Assert.True(item.IsExpense);
    }

    [Fact]
    public void CategoryService_AddRoot_AppendsSort_AndDeleteCascades()
    {
        var service = new CategoryService(_test.Db);
        var rootsBefore = _test.Db.Categories.GetRoots(CategoryKind.Expense);

        var created = service.AddRoot("Хобби", CategoryKind.Expense, "🎨");
        Assert.Equal("🎨", created.Icon);
        Assert.Equal(rootsBefore.Max(c => c.Sort) + 1, created.Sort);

        var child = service.AddChild(created.Id, "Настолки", null);
        Assert.Equal(created.Id, child.ParentId);

        service.DeleteWithChildren(created);

        Assert.Null(_test.Db.Categories.Get(created.Id));
        Assert.Null(_test.Db.Categories.Get(child.Id));
    }

    [Fact]
    public void CategoryService_CannotDeleteDefault()
    {
        var service = new CategoryService(_test.Db);
        var def = _test.Db.Categories.GetRoots(CategoryKind.Expense).First(c => c.Name == "Продукты");

        Assert.Throws<InvalidOperationException>(() => service.DeleteWithChildren(def));
    }

    private void SaveTx(string accountId, string kind, long amount, string? categoryId, long date, string? transferTo = null)
    {
        _test.Db.Transactions.Save(new Transaction
        {
            AccountId = accountId,
            CategoryId = categoryId,
            AmountMinor = amount,
            Kind = kind,
            DateUnix = date,
            TransferAccountId = transferTo,
        });
    }
}
