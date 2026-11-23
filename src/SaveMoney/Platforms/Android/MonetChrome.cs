using Android.Content.Res;
using Android.Views;
using Google.Android.Material.BottomNavigation;
using Android.Graphics.Drawables;
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
        StyleWindow(window);

        // Модальные страницы MAUI живут в окне DialogFragment со своей статус-строкой:
        // без этого «Выбор категории» оставался с системным синим акцентом.
        if (activity is AndroidX.Fragment.App.FragmentActivity fragmentActivity)
            foreach (var fragment in fragmentActivity.SupportFragmentManager.Fragments)
                if (fragment is AndroidX.Fragment.App.DialogFragment { Dialog.Window: { } dialogWindow })
                    StyleWindow(dialogWindow);
    }

    private static void StyleWindow(global::Android.Views.Window window)
    {
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
        // Страницы с навбаром Shell: на Android 15 статус-строка прозрачная, под ней
        // рисуют AppBarLayout/CoordinatorLayout — по умолчанию системным акцентом
        // (colorPrimary = system_accent1_600), отсюда «синяя полоса» при своей палитре.
        if (root is AndroidX.CoordinatorLayout.Widget.CoordinatorLayout coordinator)
            coordinator.SetStatusBarBackgroundColor(Color("Surface"));
        if (root is Google.Android.Material.AppBar.AppBarLayout appBar)
        {
            appBar.SetStatusBarForegroundColor(Color("Surface"));
            appBar.SetBackgroundColor(Color("Surface"));
        }

        if (root is BottomNavigationView nav)
        {
            var density = nav.Resources?.DisplayMetrics?.Density ?? 1;
            nav.SetBackgroundColor(Color("SurfaceContainer"));
            var labelStyle = nav.Resources?.Configuration?.FontScale > 1.4f
                ? Resource.Style.MonetNavigationLabelLargeText : Resource.Style.MonetNavigationLabel;
            nav.ItemTextAppearanceActive = labelStyle;
            nav.ItemTextAppearanceInactive = labelStyle;
            nav.LabelVisibilityMode = 1; // LABEL_VISIBILITY_LABELED
            // Highlight the complete destination: icon AND label, not just the icon pill.
            nav.ItemActiveIndicatorEnabled = false;
            var fill = new GradientDrawable();
            fill.SetColor(Color("SecondaryContainer"));
            fill.SetCornerRadius(24 * density);
            var statesBackground = new StateListDrawable();
            var inset = (int)(4 * density);
            statesBackground.AddState([global::Android.Resource.Attribute.StateChecked], new InsetDrawable(fill, inset));
            statesBackground.AddState([], new ColorDrawable(global::Android.Graphics.Color.Transparent));
            nav.ItemBackground = statesBackground;
            int[][] states = [[global::Android.Resource.Attribute.StateChecked], []];
            nav.ItemIconTintList = new ColorStateList(states, [Color("OnSecondaryContainer").ToArgb(), Color("OnSurfaceVariant").ToArgb()]);
            nav.ItemTextColor = new ColorStateList(states, [Color("OnSecondaryContainer").ToArgb(), Color("OnSurfaceVariant").ToArgb()]);
            return;
        }
        if (root is global::Android.Views.ViewGroup group)
            for (var i = 0; i < group.ChildCount; i++)
                if (group.GetChildAt(i) is { } child) StyleNavigation(child);
    }
}
