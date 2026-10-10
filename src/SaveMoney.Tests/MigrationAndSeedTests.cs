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

    [Fact]
    public void Migration_V2_ConvertsEmojiIcons_ToKeys()
    {
        var path = _test.Db.Connection.DatabasePath;

        // Эмулируем базу версии 1: эмодзи-иконки и user_version = 1.
        _test.Db.Connection.Execute("UPDATE categories SET icon = '🛒' WHERE name = 'Продукты'");
        _test.Db.Connection.Execute("UPDATE categories SET icon = '🎁' WHERE name IN ('Подарки', 'Кэшбэк и бонусы')");
        _test.Db.Connection.Execute("PRAGMA user_version = 1");
        _test.Db.Dispose();

        using var migrated = new AppDatabase(path);
        var icons = migrated.Categories.GetAllActive()
            .Where(c => c.Name is "Продукты" or "Подарки" or "Кэшбэк и бонусы")
            .ToDictionary(c => c.Name, c => c.Icon);

        Assert.Equal(IconKeys.Groceries, icons["Продукты"]);
        Assert.Equal(IconKeys.Gift, icons["Подарки"]);
        // 🎁 у кэшбэка общим правилом ушёл в gift, спец-правило исправляет на bonus.
        Assert.Equal(IconKeys.Bonus, icons["Кэшбэк и бонусы"]);
        Assert.All(migrated.Categories.GetAllActive(), c =>
            Assert.False(c.Icon?.Any(ch => ch > '\u2600') == true, $"Эмодзи остался: {c.Icon} у «{c.Name}»"));
    }
}
