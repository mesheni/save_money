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
            using var dialog = new Android.App.DatePickerDialog(activity,
                (_, args) => completion.TrySetResult(args.Date.Date),
                selected.Year, selected.Month - 1, selected.Day);
            dialog.DismissEvent += (_, _) => completion.TrySetResult(null);
            dialog.Show();
            return await completion.Task;
#else
            return null;
#endif
        }
        finally { _isOpen = false; }
    }
}
