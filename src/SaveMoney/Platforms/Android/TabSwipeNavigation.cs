using global::Android.Views;
using SaveMoney.Core.Services;
using SaveMoney.Views;

namespace SaveMoney.Platforms.Android;

/// <summary>Observes touch without stealing scrolling or native control gestures.</summary>
public sealed class TabSwipeNavigation
{
    private float _startX, _startY, _vertical;
    private bool _tracking, _switching;
    private Page? _startPage;
    private static readonly string[] Routes = ["quickadd", "history", "reports", "more"];

    public void Observe(MotionEvent ev, global::Android.Views.View? decor, float density)
    {
        var shell = Shell.Current;
        if (ev.ActionMasked == MotionEventActions.Down)
        {
            _startX = ev.RawX;
            _startY = ev.RawY;
            _vertical = 0;
            _startPage = shell?.CurrentPage;
            _tracking = !_switching && decor is not null && IsRoot(shell)
                && ev.RawX > 24 * density && ev.RawX < decor.Width - 24 * density
                && IsInsidePage(_startPage, ev.RawX, ev.RawY)
                && _startPage is ContentPage { Content.Handler.PlatformView: global::Android.Views.View content }
                && !IsExcluded(content, ev.RawX, ev.RawY);
            return;
        }
        if (ev.PointerCount > 1 || ev.ActionMasked == MotionEventActions.Cancel)
            _tracking = false;
        if (!_tracking) return;
        _vertical = Math.Max(_vertical, Math.Abs(ev.RawY - _startY));
        if (ev.ActionMasked != MotionEventActions.Up) return;
        _tracking = false;
        if (shell?.CurrentPage != _startPage || !IsRoot(shell)) return;
        var step = TabSwipePolicy.GetStep((ev.RawX - _startX) / density, _vertical / density);
        var index = _startPage switch { QuickAddPage => 0, HistoryPage => 1, ReportsPage => 2, MorePage => 3, _ => -1 };
        var target = index + step;
        if (step != 0 && index >= 0 && target >= 0 && target < Routes.Length)
            _ = SwitchAsync(shell!, Routes[target]);
    }

    private async Task SwitchAsync(Shell shell, string route)
    {
        _switching = true;
        try { await shell.GoToAsync($"//{route}", MotionEnabled()); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        finally { _switching = false; }
    }

    private static bool MotionEnabled() => Helpers.Motion.AnimationsEnabled;

    private static bool IsRoot(Shell? shell) => shell is not null
        && shell.Navigation.ModalStack.Count == 0
        && shell.CurrentPage?.Navigation.NavigationStack.Count <= 1
        && shell.CurrentPage is QuickAddPage or HistoryPage or ReportsPage or MorePage;

    private static bool IsInsidePage(Page? page, float x, float y) =>
        page is ContentPage { Content.Handler.PlatformView: global::Android.Views.View native } && Contains(native, x, y);

    private static bool Contains(global::Android.Views.View view, float x, float y)
    {
        using var rect = new global::Android.Graphics.Rect();
        return view.GetGlobalVisibleRect(rect) && rect.Contains((int)x, (int)y);
    }

    private static bool IsExcluded(global::Android.Views.View view, float x, float y)
    {
        if (!Contains(view, x, y)) return false;
        var name = view.GetType().Name;
        if (view is global::Android.Widget.HorizontalScrollView or global::Android.Widget.EditText
            or global::Android.Widget.SeekBar or global::Android.Webkit.WebView
            || name.Contains("SwipeView", StringComparison.OrdinalIgnoreCase)
            || name.Contains("SKCanvas", StringComparison.OrdinalIgnoreCase)
            || name.Contains("SKGL", StringComparison.OrdinalIgnoreCase)
            || view.CanScrollHorizontally(-1) || view.CanScrollHorizontally(1)) return true;
        if (view is ViewGroup group)
            for (var i = group.ChildCount - 1; i >= 0; i--)
                if (group.GetChildAt(i) is { } child && IsExcluded(child, x, y)) return true;
        return false;
    }
}
