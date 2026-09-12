using SaveMoney.Core.Models;
using Xunit;

namespace SaveMoney.Tests;

public class MerchantRuleAndSettingTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    [Fact]
    public void Match_FindsRule_CaseInsensitive_AndPrefersMostUsed()
    {
        var groceries = _test.Db.Categories.GetRoots(CategoryKind.Expense).First(c => c.Name == "Продукты");
        var cafe = _test.Db.Categories.GetRoots(CategoryKind.Expense).First(c => c.Name == "Кафе и рестораны");

        var weak = new MerchantRule { Pattern = "Ленина", CategoryId = cafe.Id, UseCount = 1 };
        var strong = new MerchantRule { Pattern = "Магнит", CategoryId = groceries.Id, UseCount = 10 };
        _test.Db.MerchantRules.Save(weak);
        _test.Db.MerchantRules.Save(strong);

        // Подходят оба правила (улица и магазин), побеждает то, что использовалось чаще.
        var match = _test.Db.MerchantRules.Match("МАГНИТ пр. Ленина");

        Assert.NotNull(match);
        Assert.Equal(groceries.Id, match.CategoryId);
    }

    [Fact]
    public void Match_ReturnsNull_ForEmptyPayee()
    {
        Assert.Null(_test.Db.MerchantRules.Match(""));
    }

    [Fact]
    public void Settings_Roundtrip_AndFallbacks()
    {
        Assert.Equal(5, _test.Db.Settings.GetInt(SettingKeys.PaydayDay, 5));

        _test.Db.Settings.SetInt(SettingKeys.PaydayDay, 10);
        _test.Db.Settings.SetLong(SettingKeys.CycleAnchorUnix, 1_700_000_000);
        _test.Db.Settings.SetBool(SettingKeys.IsPro, true);

        Assert.Equal(10, _test.Db.Settings.GetInt(SettingKeys.PaydayDay, 5));
        Assert.Equal(1_700_000_000, _test.Db.Settings.GetLong(SettingKeys.CycleAnchorUnix, 0));
        Assert.True(_test.Db.Settings.GetBool(SettingKeys.IsPro, false));
    }

    [Fact]
    public void Settings_UpdateExistingKey()
    {
        _test.Db.Settings.Set(SettingKeys.Theme, "light");
        _test.Db.Settings.Set(SettingKeys.Theme, "dark");

        Assert.Equal("dark", _test.Db.Settings.Get(SettingKeys.Theme));
    }
}
