namespace SaveMoney.Platforms.Android;

/// <summary>Read Android's palette, never wallpaper pixels or user files.</summary>
public static class DeviceColors
{
    public static void Apply(IDictionary<string, string> scheme, bool dark)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(31)) return;
        var resources = global::Android.App.Application.Context.Resources;
        if (resources is null) return;
        // Android 12/13 expose tonal palettes. Android 14+ also expose complete role colors.
        var tones = new Dictionary<string, (string Palette, int Light, int Dark)>
        {
            ["Primary"] = ("accent1", 600, 200), ["OnPrimary"] = ("accent1", 0, 800),
            ["PrimaryContainer"] = ("accent1", 100, 700), ["OnPrimaryContainer"] = ("accent1", 900, 100),
            ["Secondary"] = ("accent2", 600, 200), ["OnSecondary"] = ("accent2", 0, 800),
            ["SecondaryContainer"] = ("accent2", 100, 700), ["OnSecondaryContainer"] = ("accent2", 900, 100),
            ["Tertiary"] = ("accent3", 600, 200), ["OnTertiary"] = ("accent3", 0, 800),
            ["TertiaryContainer"] = ("accent3", 100, 700), ["OnTertiaryContainer"] = ("accent3", 900, 100),
            ["Surface"] = ("neutral1", 10, 900), ["OnSurface"] = ("neutral1", 900, 100),
            ["OnSurfaceVariant"] = ("neutral2", 700, 200),
            ["SurfaceContainerLowest"] = ("neutral1", 0, 1000),
            ["SurfaceContainerLow"] = ("neutral1", 50, 900),
            ["SurfaceContainer"] = ("neutral1", 100, 900),
            ["SurfaceContainerHigh"] = ("neutral1", 100, 800),
            ["SurfaceContainerHighest"] = ("neutral1", 200, 800),
            ["Outline"] = ("neutral2", 500, 400), ["OutlineVariant"] = ("neutral2", 200, 700),
        };
        foreach (var (role, tone) in tones)
        {
            var roleName = System.Text.RegularExpressions.Regex.Replace(role, "(?<!^)[A-Z]", "_$0").ToLowerInvariant();
            var id = OperatingSystem.IsAndroidVersionAtLeast(34)
                ? resources.GetIdentifier($"system_{roleName}_{(dark ? "dark" : "light")}", "color", "android") : 0;
            if (id == 0)
                id = resources.GetIdentifier($"system_{tone.Palette}_{(dark ? tone.Dark : tone.Light)}", "color", "android");
            if (id != 0)
            {
                var color = resources.GetColor(id, null);
                scheme[role] = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
        }
    }
}
