using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;
using SaveMoney.Helpers;

namespace SaveMoney.Droid;

/// <summary>
/// Виджет быстрого ввода 4×1: пресеты «＋100 / ＋500 / ＋1000 / иконка ввода».
/// Каждая кнопка — PendingIntent на MainActivity с extra суммы; экран ввода
/// открывается уже с набранной суммой (остаётся выбрать категорию — 2 касания).
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false, Label = "Быстрый ввод")]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate, Intent.ActionWallpaperChanged, Intent.ActionConfigurationChanged })]
[MetaData("android.appwidget.provider", Resource = "@xml/widget_info")]
public class QuickAddWidgetProvider : AppWidgetProvider
{
    public override void OnUpdate(Context? context, AppWidgetManager? appWidgetManager, int[]? appWidgetIds)
    {
        if (context is null || appWidgetManager is null || appWidgetIds is null)
        {
            return;
        }

        foreach (var id in appWidgetIds)
        {
            var views = new RemoteViews(context.PackageName, Resource.Layout.widget_quick_add);

            views.SetOnClickPendingIntent(Resource.Id.widget_btn_100, TapIntent(context, "100"));
            views.SetOnClickPendingIntent(Resource.Id.widget_btn_500, TapIntent(context, "500"));
            views.SetOnClickPendingIntent(Resource.Id.widget_btn_1000, TapIntent(context, "1000"));
            views.SetOnClickPendingIntent(Resource.Id.widget_btn_custom, TapIntent(context, ""));

            Style(context, views);
            appWidgetManager.UpdateAppWidget(id, views);
        }
    }

    public static void SaveAppearance(string key, string source)
    {
        var context = global::Android.App.Application.Context;
        using var prefs = context.GetSharedPreferences("monet_widget", FileCreationMode.Private);
        prefs?.Edit()?.PutString("key", key)?.PutString("source", source)?.Apply();
        RefreshAll(context);
    }

    public static void RefreshAll(Context context)
    {
        var manager = AppWidgetManager.GetInstance(context);
        var ids = manager?.GetAppWidgetIds(new ComponentName(context, Java.Lang.Class.FromType(typeof(QuickAddWidgetProvider))));
        if (ids is { Length: > 0 }) new QuickAddWidgetProvider().OnUpdate(context, manager, ids);
    }

    public override void OnReceive(Context? context, Intent? intent)
    {
        base.OnReceive(context, intent);
        if (context is not null && intent?.Action is Intent.ActionWallpaperChanged or Intent.ActionConfigurationChanged)
            RefreshAll(context);
    }

    private static void Style(Context context, RemoteViews views)
    {
        using var prefs = context.GetSharedPreferences("monet_widget", FileCreationMode.Private);
        var key = MonetScheme.NormalizeKey(prefs?.GetString("key", MonetScheme.DefaultKey));
        var device = prefs?.GetString("source", OperatingSystem.IsAndroidVersionAtLeast(31) ? MonetScheme.Device : MonetScheme.Manual) == MonetScheme.Device && OperatingSystem.IsAndroidVersionAtLeast(31);
        var dark = (context.Resources!.Configuration!.UiMode & global::Android.Content.Res.UiMode.NightMask) == global::Android.Content.Res.UiMode.NightYes;
        var colors = new Dictionary<string, string>(MonetScheme.Create(key, dark));
        if (device) SaveMoney.Platforms.Android.DeviceColors.Apply(colors, dark);
        var (background, button) = key switch
        {
            "sage" => (Resource.Drawable.widget_sage_bg, Resource.Drawable.widget_sage_btn_bg),
            "blue" => (Resource.Drawable.widget_blue_bg, Resource.Drawable.widget_blue_btn_bg),
            "lavender" => (Resource.Drawable.widget_lavender_bg, Resource.Drawable.widget_lavender_btn_bg),
            "rose" => (Resource.Drawable.widget_rose_bg, Resource.Drawable.widget_rose_btn_bg),
            _ => (Resource.Drawable.widget_apricot_bg, Resource.Drawable.widget_apricot_btn_bg),
        };
        views.SetInt(Resource.Id.widget_root, "setBackgroundResource", device ? Resource.Drawable.widget_bg : background);
        foreach (var id in new[] { Resource.Id.widget_btn_100, Resource.Id.widget_btn_500, Resource.Id.widget_btn_1000, Resource.Id.widget_btn_custom })
        {
            views.SetInt(id, "setBackgroundResource", device ? Resource.Drawable.widget_btn_bg : button);
            if (id != Resource.Id.widget_btn_custom)
                views.SetTextColor(id, global::Android.Graphics.Color.ParseColor(colors["OnPrimaryContainer"]));
        }
        views.SetInt(Resource.Id.widget_btn_custom, "setColorFilter", global::Android.Graphics.Color.ParseColor(colors["OnPrimaryContainer"]).ToArgb());
    }

    private static PendingIntent TapIntent(Context context, string amount) =>
        PendingIntent.GetActivity(
            context,
            string.IsNullOrEmpty(amount) ? 1 : int.Parse(amount),
            new Intent(context, typeof(MainActivity))
                .SetAction(Intent.ActionView)
                .PutExtra(MainActivity.ExtraQuickAmount, amount),
            PendingIntentFlags.UpdateCurrent | (OperatingSystem.IsAndroidVersionAtLeast(23) ? PendingIntentFlags.Immutable : 0));
}
