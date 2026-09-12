using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.Widget;

namespace SaveMoney.Droid;

/// <summary>
/// Виджет быстрого ввода 4×1: пресеты «＋100 / ＋500 / ＋1000 / ✎».
/// Каждая кнопка — PendingIntent на MainActivity с extra суммы; экран ввода
/// открывается уже с набранной суммой (остаётся выбрать категорию — 2 касания).
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = false, Label = "Быстрый ввод")]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate })]
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

            appWidgetManager.UpdateAppWidget(id, views);
        }
    }

    private static PendingIntent TapIntent(Context context, string amount) =>
        PendingIntent.GetActivity(
            context,
            string.IsNullOrEmpty(amount) ? 1 : int.Parse(amount),
            new Intent(context, typeof(MainActivity))
                .SetAction(Intent.ActionView)
                .PutExtra(MainActivity.ExtraQuickAmount, amount),
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
}
