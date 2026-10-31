using SaveMoney.Helpers;
using SaveMoney.Core.Models;

namespace SaveMoney.Tests;

public class MonetSchemeTests
{
    public static IEnumerable<object[]> Schemes => MonetScheme.Options.SelectMany(p =>
        new[] { new object[] { p.Key, false }, new object[] { p.Key, true } });

    [Theory]
    [MemberData(nameof(Schemes))]
    public void SchemesHaveCompleteRolesAndReadableText(string key, bool dark)
    {
        var colors = MonetScheme.Create(key, dark);
        Assert.Equal(32, colors.Count);
        foreach (var hex in colors.Values) Assert.Matches("^#[0-9A-F]{6}$", hex);
        foreach (var role in new[] { "Primary", "Secondary", "Tertiary", "Error" })
        {
            Readable(colors["On" + role], colors[role]);
            Readable(colors["On" + role + "Container"], colors[role + "Container"]);
        }
        foreach (var surface in new[] { "Surface", "SurfaceContainerLowest", "SurfaceContainerLow", "SurfaceContainer", "SurfaceContainerHigh", "SurfaceContainerHighest" })
        {
            Readable(colors["OnSurface"], colors[surface]);
            Readable(colors["OnSurfaceVariant"], colors[surface]);
            Readable(colors["Primary"], colors[surface]);
            Readable(colors["Income"], colors[surface]);
            Readable(colors["Warning"], colors[surface]);
        }
    }

    [Theory]
    [InlineData(null, false, true, "device")]
    [InlineData(null, false, false, "manual")]
    [InlineData(null, true, true, "manual")]
    [InlineData("manual", false, true, "manual")]
    [InlineData("device", true, true, "device")]
    [InlineData("device", true, false, "device")]
    [InlineData("unknown", true, true, "manual")]
    public void SourceSelectionPreservesExplicitChoice(string? saved, bool existing, bool supported, string expected) =>
        Assert.Equal(expected, MonetScheme.ResolveSource(saved, existing, supported));

    [Fact]
    public void InvalidOrMissingKeyUsesApricotFallback()
    {
        Assert.Equal("apricot", MonetScheme.NormalizeKey(null));
        Assert.Equal("apricot", MonetScheme.NormalizeKey("unknown"));
        Assert.Equal(MonetScheme.Create("apricot", true), MonetScheme.Create("unknown", true));
    }

    [Fact]
    public void ColorSourceDoesNotReplaceSavedThemeOrAccent()
    {
        using var db = new TestDatabase();
        db.Db.Settings.Set(SettingKeys.Theme, "dark");
        db.Db.Settings.Set(SettingKeys.Accent, "sage");
        var source = MonetScheme.ResolveSource(db.Db.Settings.Get(SettingKeys.ColorSource), true, true);
        db.Db.Settings.Set(SettingKeys.ColorSource, source);
        Assert.Equal("manual", db.Db.Settings.Get(SettingKeys.ColorSource));
        Assert.Equal("dark", db.Db.Settings.Get(SettingKeys.Theme));
        Assert.Equal("sage", db.Db.Settings.Get(SettingKeys.Accent));
    }

    private static void Readable(string text, string background)
    {
        static double Luminance(string hex)
        {
            static double Channel(string value)
            {
                var s = Convert.ToInt32(value, 16) / 255d;
                return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4);
            }
            return .2126 * Channel(hex[1..3]) + .7152 * Channel(hex[3..5]) + .0722 * Channel(hex[5..7]);
        }
        var a = Luminance(text);
        var b = Luminance(background);
        var contrast = (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
        Assert.True(contrast >= 4.5, $"{text} on {background}: {contrast:F2}:1");
    }
}
