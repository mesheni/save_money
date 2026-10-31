using Microsoft.Maui.Graphics;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Helpers;

/// <summary>One resolved Material 3 scheme shared by XAML, charts and Android chrome.</summary>
public static class ThemePalette
{
    public const string DefaultKey = MonetScheme.DefaultKey;
    public static IReadOnlyList<(string Key, string Name, string Preview)> Options => MonetScheme.Options;
    public static string SelectedKey { get; private set; } = DefaultKey;
    public static string Source { get; private set; } = MonetScheme.Manual;
    public static event EventHandler? Changed;
    public static int Revision { get; private set; }
    public static bool SupportsDeviceColors
    {
        get
        {
#if ANDROID
            return OperatingSystem.IsAndroidVersionAtLeast(31);
#else
            return false;
#endif
        }
    }

    public static void Initialize(AppDatabase db)
    {
        var saved = db.Settings.Get(SettingKeys.ColorSource);
        var existing = db.Settings.Get(SettingKeys.Accent) is not null || db.Settings.Get(SettingKeys.Theme) is not null
            || db.Accounts.GetAll().Count > 0;
        Source = MonetScheme.ResolveSource(saved, existing, SupportsDeviceColors);
        db.Settings.Set(SettingKeys.ColorSource, Source);
        Apply(db.Settings.Get(SettingKeys.Accent), Application.Current?.RequestedTheme ?? AppTheme.Light);
    }

    public static void SetSource(string source)
    {
        Source = source == MonetScheme.Device ? MonetScheme.Device : MonetScheme.Manual;
        Apply(SelectedKey, Application.Current?.RequestedTheme ?? AppTheme.Light);
    }

    public static void Apply(string? key, AppTheme theme)
    {
        SelectedKey = MonetScheme.NormalizeKey(key);
        if (Application.Current?.Resources is not { } resources) return;
        var dark = theme == AppTheme.Dark;
        var scheme = new Dictionary<string, string>(MonetScheme.Create(SelectedKey, dark));
#if ANDROID
        if (Source == MonetScheme.Device && SupportsDeviceColors)
            Platforms.Android.DeviceColors.Apply(scheme, dark);
#endif
        var changed = false;
        foreach (var (role, hex) in scheme)
        {
            var color = Color.FromArgb(hex);
            if (!resources.TryGetValue(role, out var old) || !color.Equals(old))
            {
                resources[role] = color;
                changed = true;
            }
        }
        // Compatibility aliases for code consuming the previous accent contract.
        resources["Accent"] = resources["Primary"];
        resources["OnAccent"] = resources["OnPrimary"];
        resources["AccentText"] = resources["Primary"];
        resources["AccentSoft"] = resources["PrimaryContainer"];
        RefreshPlatform();
        Application.Current.Dispatcher.Dispatch(RefreshPlatform);
        if (changed)
        {
#if ANDROID
            Droid.QuickAddWidgetProvider.SaveAppearance(SelectedKey, Source);
#endif
            Revision++;
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }

    public static Color Get(string role) => Application.Current?.Resources.TryGetValue(role, out var value) == true && value is Color color
        ? color : Color.FromArgb(MonetScheme.Create(SelectedKey, false)[role]);
    public static Color AccentColor => Get("Primary");

    public static void RefreshPlatform()
    {
#if ANDROID
        Platforms.Android.MonetChrome.Refresh();
#endif
    }
}
