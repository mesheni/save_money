using Microsoft.Maui.Graphics;

namespace SaveMoney.Helpers;

/// <summary>Единый источник выбранного акцента для XAML, графиков и платформенных элементов.</summary>
public static class ThemePalette
{
    public const string DefaultKey = "apricot";

    public static IReadOnlyList<(string Key, string Name, string Preview)> Options { get; } =
    [
        ("apricot", "Абрикосовый", "#D9A18A"),
        ("sage", "Шалфейный", "#91B5A2"),
        ("blue", "Голубой", "#92B7CE"),
        ("lavender", "Лавандовый", "#ADA6CE"),
        ("rose", "Розовый", "#D5A1B3"),
    ];

    private static readonly Dictionary<string, (string Light, string Dark, string TextLight, string TextDark, string SoftLight, string SoftDark)> Palettes = new()
    {
        ["apricot"] = ("#AE6241", "#D9A18A", "#96502F", "#F1BBA2", "#F8EAE2", "#47352F"),
        ["sage"] = ("#487B63", "#91B5A2", "#37694F", "#B7D9C3", "#E6F1E9", "#2B4036"),
        ["blue"] = ("#477C9C", "#92B7CE", "#346A8B", "#B8D9EA", "#E6F0F6", "#2A3C48"),
        ["lavender"] = ("#76699B", "#ADA6CE", "#625487", "#D0C8EC", "#EEEBF6", "#383447"),
        ["rose"] = ("#A76179", "#D5A1B3", "#904A64", "#F0BED0", "#F8E9EF", "#46323A"),
    };

    public static string SelectedKey { get; private set; } = DefaultKey;

    public static void Apply(string? key, AppTheme theme)
    {
        SelectedKey = key is not null && Palettes.ContainsKey(key) ? key : DefaultKey;
        if (Application.Current?.Resources is not { } resources)
            return;

        var palette = Palettes[SelectedKey];
        var dark = theme == AppTheme.Dark;
        resources["Accent"] = Color.FromArgb(dark ? palette.Dark : palette.Light);
        resources["OnAccent"] = Color.FromArgb(dark ? "#1D2529" : "#FFFFFF");
        resources["AccentText"] = Color.FromArgb(dark ? palette.TextDark : palette.TextLight);
        resources["AccentSoft"] = Color.FromArgb(dark ? palette.SoftDark : palette.SoftLight);
        RefreshPlatform();
    }

    public static void RefreshPlatform()
    {
#if ANDROID
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.Window is not { } window)
            return;

        var palette = Palettes[SelectedKey];
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var statusColor = dark ? palette.TextLight : palette.Light;
        window.SetStatusBarColor(global::Android.Graphics.Color.ParseColor(statusColor));
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.R)
        {
            const int lightStatusBars = 8; // WindowInsetsController.APPEARANCE_LIGHT_STATUS_BARS
            window.DecorView.WindowInsetsController?.SetSystemBarsAppearance(dark ? 0 : lightStatusBars, lightStatusBars);
        }
        if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.M)
        {
            var flags = (int)window.DecorView.SystemUiVisibility;
            var lightStatusBar = (int)global::Android.Views.SystemUiFlags.LightStatusBar;
            window.DecorView.SystemUiVisibility = (global::Android.Views.StatusBarVisibility)(dark
                ? flags & ~lightStatusBar
                : flags | lightStatusBar);
        }
#endif
    }

    public static Color AccentColor => Application.Current?.Resources.TryGetValue("Accent", out var value) == true && value is Color color
        ? color : Color.FromArgb("#AE6241");
}
