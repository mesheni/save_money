namespace SaveMoney.Helpers;

/// <summary>Material dialogs use the same resolved palette as MAUI pages.</summary>
public static class MonetDialogs
{
    public static async Task<int?> ChooseIndexAsync(string title, string[] options, int selectedIndex)
    {
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity is not null && !activity.IsFinishing)
        {
            var result = new TaskCompletionSource<int?>();
            using var context = CreateContext(activity);
            using var builder = new Google.Android.Material.Dialog.MaterialAlertDialogBuilder(context);
            builder.SetTitle(title);
            using var adapter = new Choices(context, options, true);
            builder.SetSingleChoiceItems(adapter, selectedIndex, (_, args) => result.TrySetResult(args.Which));
            builder.SetNegativeButton("Отмена", (_, _) => result.TrySetResult(null));
            using var dialog = builder.Create();
            dialog.DismissEvent += (_, _) => result.TrySetResult(null);
            dialog.Show();
            Style(dialog);
            var index = await result.Task;
            dialog.Dismiss();
            return index;
        }
#endif
        var labels = options.Select((text, index) => $"{index + 1}. {text}").ToArray();
        var picked = await Shell.Current.DisplayActionSheetAsync(title, "Отмена", null, labels);
        var found = Array.IndexOf(labels, picked);
        return found < 0 ? null : found;
    }

    public static async Task AlertAsync(string title, string message, string cancel) =>
        await AlertAsync(title, message, cancel, null);

    public static async Task<bool> AlertAsync(string title, string message, string accept, string? cancel)
    {
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity is not null && !activity.IsFinishing)
        {
            var result = new TaskCompletionSource<bool>();
            using var context = CreateContext(activity);
            using var builder = new Google.Android.Material.Dialog.MaterialAlertDialogBuilder(context);
            builder.SetTitle(title);
            builder.SetMessage(message);
            builder.SetPositiveButton(accept, (_, _) => result.TrySetResult(true));
            if (cancel is not null) builder.SetNegativeButton(cancel, (_, _) => result.TrySetResult(false));
            using var dialog = builder.Create();
            dialog.DismissEvent += (_, _) => result.TrySetResult(false);
            dialog.Show();
            Style(dialog);
            return await result.Task;
        }
#endif
        if (cancel is null) { await Shell.Current.DisplayAlertAsync(title, message, accept); return true; }
        return await Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
    }

    public static async Task<string?> ChooseAsync(string title, string? cancel, string? destruction, params string[] buttons)
    {
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity is not null && !activity.IsFinishing)
        {
            var result = new TaskCompletionSource<string?>();
            using var context = CreateContext(activity);
            using var builder = new Google.Android.Material.Dialog.MaterialAlertDialogBuilder(context);
            builder.SetTitle(title);
            using var adapter = new Choices(context, buttons);
            builder.SetAdapter(adapter, (_, args) => result.TrySetResult(buttons[args.Which]));
            if (cancel is not null) builder.SetNegativeButton(cancel, (_, _) => result.TrySetResult(cancel));
            if (destruction is not null) builder.SetNeutralButton(destruction, (_, _) => result.TrySetResult(destruction));
            using var dialog = builder.Create();
            dialog.DismissEvent += (_, _) => result.TrySetResult(null);
            dialog.Show();
            Style(dialog);
            return await result.Task;
        }
#endif
        return await Shell.Current.DisplayActionSheetAsync(title, cancel, destruction, buttons);
    }

#if ANDROID
    internal static global::Android.Views.ContextThemeWrapper CreateContext(global::Android.App.Activity activity, bool calendar = false)
    {
        using var config = new global::Android.Content.Res.Configuration(activity.Resources!.Configuration);
        config.UiMode = (config.UiMode & ~global::Android.Content.Res.UiMode.NightMask)
            | (Application.Current?.RequestedTheme == AppTheme.Dark
                ? global::Android.Content.Res.UiMode.NightYes : global::Android.Content.Res.UiMode.NightNo);
        var calendarTheme = ThemePalette.Source == MonetScheme.Device && ThemePalette.SupportsDeviceColors
            ? Resource.Style.MonetCalendarTheme : ThemePalette.SelectedKey switch
            {
                "sage" => Resource.Style.MonetCalendarSageTheme,
                "blue" => Resource.Style.MonetCalendarBlueTheme,
                "lavender" => Resource.Style.MonetCalendarLavenderTheme,
                "rose" => Resource.Style.MonetCalendarRoseTheme,
                _ => Resource.Style.MonetCalendarApricotTheme,
            };
        var wrapper = new global::Android.Views.ContextThemeWrapper(activity, calendar ? calendarTheme : Resource.Style.MonetDialogTheme);
        wrapper.ApplyOverrideConfiguration(config);
        return wrapper;
    }

    private static global::Android.Graphics.Color Native(string role) => global::Android.Graphics.Color.ParseColor(ThemePalette.Get(role).ToArgbHex());

    internal static void Style(AndroidX.AppCompat.App.AlertDialog dialog)
    {
        var background = new global::Android.Graphics.Drawables.GradientDrawable();
        background.SetColor(Native("SurfaceContainerHigh"));
        background.SetCornerRadius(28 * (dialog.Context.Resources?.DisplayMetrics?.Density ?? 1));
        dialog.Window?.SetBackgroundDrawable(background);
        if (dialog.Window?.DecorView is { } root) StyleText(root);
        foreach (var which in new[] { -1, -2, -3 }) dialog.GetButton(which)?.SetTextColor(Native("Primary"));
    }

    private static void StyleText(global::Android.Views.View view)
    {
        if (view is global::Android.Widget.TextView text) text.SetTextColor(Native("OnSurface"));
        if (view is global::Android.Views.ViewGroup group)
            for (var i = 0; i < group.ChildCount; i++)
                if (group.GetChildAt(i) is { } child) StyleText(child);
    }

    private sealed class Choices(global::Android.Content.Context context, string[] values, bool singleChoice = false)
        : global::Android.Widget.ArrayAdapter<string>(context, singleChoice ? global::Android.Resource.Layout.SimpleListItemSingleChoice : global::Android.Resource.Layout.SimpleListItem1, values)
    {
        public override global::Android.Views.View GetView(int position, global::Android.Views.View? convertView, global::Android.Views.ViewGroup parent)
        {
            var view = base.GetView(position, convertView, parent);
            if (view is global::Android.Widget.TextView text)
            {
                text.SetTextColor(Native("OnSurface"));
                text.SetSingleLine(false);
                text.SetMinHeight((int)(56 * (Context.Resources?.DisplayMetrics?.Density ?? 1)));
            }
            if (view is global::Android.Widget.CheckedTextView check)
                check.CheckMarkTintList = global::Android.Content.Res.ColorStateList.ValueOf(Native("Primary"));
            return view;
        }
    }
#endif
}
