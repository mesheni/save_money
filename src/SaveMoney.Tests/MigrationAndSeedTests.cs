using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using Xunit;

namespace SaveMoney.Tests;

public class MigrationAndSeedTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    [Fact]
    public void SchemaVersion_IsCurrent()
    {
        var version = _test.Db.Connection.ExecuteScalar<int>("PRAGMA user_version");
        Assert.Equal(DbMigrator.CurrentVersion, version);
    }

    [Fact]
    public void Seed_CreatesDefaultCategories_WithSubcategories()
    {
        var all = _test.Db.Categories.GetAllActive();

        Assert.True(all.Count >= 60, $"Ожидалось >= 60 категорий, получено {all.Count}");
        Assert.All(all, c => Assert.True(c.IsDefault));

        var expenseRoots = _test.Db.Categories.GetRoots(CategoryKind.Expense);
        var incomeRoots = _test.Db.Categories.GetRoots(CategoryKind.Income);

        Assert.Contains(expenseRoots, c => c.Name == "Продукты");
        Assert.Contains(incomeRoots, c => c.Name == "Зарплата");

        var grocerySubs = _test.Db.Categories.GetChildren(
            expenseRoots.First(c => c.Name == "Продукты").Id);
        Assert.Contains(grocerySubs, c => c.Name == "Магнит");
    }

    [Fact]
    public void Seed_IsIdempotent_OnReopen()
    {
        var path = _test.Db.Connection.DatabasePath;
        var before = _test.Db.Categories.GetAllActive().Count;

        _test.Db.Dispose();
        using var reopened = new AppDatabase(path);

        Assert.Equal(before, reopened.Categories.GetAllActive().Count);
    }

    [Fact]
    public void DefaultCategory_CannotBeSoftDeleted()
    {
        var root = _test.Db.Categories.GetRoots(CategoryKind.Expense).First();

        Assert.Throws<InvalidOperationException>(() => _test.Db.Categories.SoftDelete(root));
    }
}
