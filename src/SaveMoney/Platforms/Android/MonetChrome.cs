using Android.Content.Res;
using Android.Views;
using Google.Android.Material.BottomNavigation;
using Google.Android.Material.Shape;
using SaveMoney.Helpers;

namespace SaveMoney.Platforms.Android;

public static class MonetChrome
{
    private static global::Android.Graphics.Color Color(string role)
    {
        var c = ThemePalette.Get(role);
        return new((byte)(c.Red * 255), (byte)(c.Green * 255), (byte)(c.Blue * 255));
    }

    public static void Refresh()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.Window is not { } window) return;
        var dark = Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;
        window.DecorView.SetBackgroundColor(Color("Surface"));
        window.SetStatusBarColor(Color("Surface"));
        window.SetNavigationBarColor(Color("SurfaceContainer"));
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            window.DecorView.WindowInsetsController?.SetSystemBarsAppearance(dark ? 0 : 24, 24);
        else if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
            var flags = (int)window.DecorView.SystemUiFlags;
            flags = dark ? flags & ~8192 : flags | 8192;
            if (OperatingSystem.IsAndroidVersionAtLeast(26)) flags = dark ? flags & ~16 : flags | 16;
            window.DecorView.SystemUiFlags = (SystemUiFlags)flags;
        }
        StyleNavigation(window.DecorView);
    }

    public static void StyleNavigation(global::Android.Views.View root)
    {
        if (root is BottomNavigationView nav)
        {
            var density = nav.Resources?.DisplayMetrics?.Density ?? 1;
            nav.SetBackgroundColor(Color("SurfaceContainer"));
            var labelStyle = nav.Resources?.Configuration?.FontScale > 1.4f
                ? Resource.Style.MonetNavigationLabelLargeText : Resource.Style.MonetNavigationLabel;
            nav.ItemTextAppearanceActive = labelStyle;
            nav.ItemTextAppearanceInactive = labelStyle;
            nav.LabelVisibilityMode = 1; // LABEL_VISIBILITY_LABELED
            nav.ItemActiveIndicatorEnabled = true;
            nav.ItemActiveIndicatorWidth = (int)(64 * density);
            nav.ItemActiveIndicatorHeight = (int)(32 * density);
            nav.ItemActiveIndicatorColor = ColorStateList.ValueOf(Color("SecondaryContainer"));
            nav.ItemActiveIndicatorShapeAppearance = ShapeAppearanceModel.InvokeBuilder()!.SetAllCornerSizes(16 * density)!.Build()!;
            int[][] states = [[global::Android.Resource.Attribute.StateChecked], []];
            nav.ItemIconTintList = new ColorStateList(states, [Color("OnSecondaryContainer").ToArgb(), Color("OnSurfaceVariant").ToArgb()]);
            nav.ItemTextColor = new ColorStateList(states, [Color("OnSurface").ToArgb(), Color("OnSurfaceVariant").ToArgb()]);
            return;
        }
        if (root is global::Android.Views.ViewGroup group)
            for (var i = 0; i < group.ChildCount; i++)
                if (group.GetChildAt(i) is { } child) StyleNavigation(child);
    }
}
