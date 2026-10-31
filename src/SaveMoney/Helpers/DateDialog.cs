namespace SaveMoney.Helpers;

/// <summary>Explicit date selection; a second tap cannot open a second dialog.</summary>
public static class DateDialog
{
    private static bool _isOpen;

    public static async Task<DateTime?> PickAsync(DateTime selected)
    {
        if (_isOpen) return null;
        _isOpen = true;
        try
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            if (activity is null || activity.IsFinishing) return null;
            var completion = new TaskCompletionSource<DateTime?>();
            using var context = MonetDialogs.CreateContext(activity, calendar: true);
            using var dialog = new Android.App.DatePickerDialog(context,
                (_, args) => completion.TrySetResult(args.Date.Date),
                selected.Year, selected.Month - 1, selected.Day);
            dialog.DismissEvent += (_, _) => completion.TrySetResult(null);
            dialog.Show();
            var background = new global::Android.Graphics.Drawables.GradientDrawable();
            background.SetColor(global::Android.Graphics.Color.ParseColor(ThemePalette.Get("SurfaceContainerHigh").ToArgbHex()));
            background.SetCornerRadius(28 * (activity.Resources?.DisplayMetrics?.Density ?? 1));
            dialog.Window?.SetBackgroundDrawable(background);
            var color = global::Android.Graphics.Color.ParseColor(ThemePalette.Get("Primary").ToArgbHex());
            dialog.GetButton(-1)?.SetTextColor(color);
            dialog.GetButton(-2)?.SetTextColor(color);
            return await completion.Task;
#else
            return null;
#endif
        }
        finally { _isOpen = false; }
    }
}
