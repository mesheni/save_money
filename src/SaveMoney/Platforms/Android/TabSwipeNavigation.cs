using global::Android.Views;
using SaveMoney.Core.Services;
using SaveMoney.Helpers;
using SaveMoney.Views;

namespace SaveMoney.Platforms.Android;

/// <summary>Axis-locked interactive paging; sends ACTION_CANCEL to the touched child on takeover.</summary>
public sealed class TabSwipeNavigation
{
    private float _startX, _startY, _vertical, _dx;
    private bool _tracking, _dragging, _settling, _swallow, _previewed, _cancelRequested;
    private Page? _startPage;
    private int _origin, _target, _direction;
    private float _density = 1;
    private VelocityTracker? _velocity;
    private PageSlide? _slide;
    private Task _prepare = Task.CompletedTask;
    private static readonly string[] Routes = ["quickadd", "history", "reports", "more"];
    public bool TookOver { get; private set; }

    public bool Handle(MotionEvent ev, global::Android.Views.View? decor, float density)
    {
        TookOver = false;
        var shell = Shell.Current;
        if (_settling || ScreenMotion.Busy) return true;
        if (ev.ActionMasked == MotionEventActions.Down)
        {
            _swallow = _previewed = _cancelRequested = false;
            _density = Math.Max(1, density);
            _startX = ev.RawX;
            _startY = ev.RawY;
            _vertical = _dx = 0;
            _startPage = shell?.CurrentPage;
            _origin = ScreenMotion.TabIndex(_startPage);
            _tracking = decor is not null && IsRoot(shell)
                && ev.RawX > 24 * density && ev.RawX < decor.Width - 24 * density
                && IsInsidePage(_startPage, ev.RawX, ev.RawY)
                && _startPage is ContentPage { Content.Handler.PlatformView: global::Android.Views.View content }
                && !IsExcluded(content, ev.RawX, ev.RawY);
            _velocity?.Recycle();
            _velocity = _tracking ? VelocityTracker.Obtain() : null;
            _velocity?.AddMovement(ev);
            return false;
        }
        if (_swallow) return true;
        if (!_tracking && !_dragging) return false;
        _velocity?.AddMovement(ev);
        if (ev.PointerCount > 1 || ev.ActionMasked == MotionEventActions.Cancel)
        {
            _tracking = false;
            if (_dragging) { _swallow = true; _ = FinishAsync(false); return true; }
            return false;
        }
        _dx = ev.RawX - _startX;
        _vertical = Math.Max(_vertical, Math.Abs(ev.RawY - _startY));
        if (!_dragging)
        {
            if (_vertical / density > 12 && _vertical >= Math.Abs(_dx)) { _tracking = false; return false; }
            if (Math.Abs(_dx) / density < 12 || Math.Abs(_dx) < _vertical * 1.5f) return false;
            _direction = _dx < 0 ? 1 : -1;
            _target = _origin + _direction;
            if (_origin < 0 || _target < 0 || _target >= Routes.Length || !IsRoot(shell)) { _tracking = false; return false; }
            _dragging = true;
            TookOver = true;
            ScreenMotion.Interactive = true;
            if (Motion.AnimationsEnabled)
            {
                _slide = PageSlide.Capture(_startPage, _direction);
                _previewed = _slide is not null;
                _prepare = _previewed ? PrepareAsync(shell!) : Task.CompletedTask;
            }
            else _prepare = Task.CompletedTask;
        }
        _slide?.SetOffset(_dx);
        if (ev.ActionMasked == MotionEventActions.Up)
        {
            _velocity?.ComputeCurrentVelocity(1000);
            var velocity = (_velocity?.XVelocity ?? 0) / _density;
            var width = (_slide?.Width ?? decor?.Width ?? 1) / _density;
            var commit = TabSwipePolicy.ShouldCommit(_dx / _density, width, velocity, _direction);
            _tracking = false;
            _ = FinishAsync(commit);
        }
        return true;
    }

    private async Task PrepareAsync(Shell shell)
    {
        // Let MainActivity deliver CANCEL to the original child before changing the hierarchy.
        await Task.Yield();
        if (_cancelRequested) return;
        await shell.GoToAsync($"//{Routes[_target]}", false);
        if (_slide is { } slide && shell.CurrentPage is { } page)
            if (!await slide.BindAsync(page)) throw new OperationCanceledException("Target page did not become ready");
    }

    private async Task FinishAsync(bool commit)
    {
        if (_settling) return;
        _settling = true;
        try
        {
            await _prepare;
            commit &= !_cancelRequested;
            if (_slide is { } slide) await slide.AnimateAsync(commit);
            commit &= !_cancelRequested;
            if (Shell.Current is { } shell)
            {
                if (!commit && _previewed) await shell.GoToAsync($"//{Routes[_origin]}", false);
                else if (commit && !_previewed) await shell.GoToAsync($"//{Routes[_target]}", false);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            if (Shell.Current is { } shell && _origin >= 0)
                try { await shell.GoToAsync($"//{Routes[_origin]}", false); } catch (Exception restore) { System.Diagnostics.Debug.WriteLine(restore); }
        }
        finally
        {
            _slide?.Dispose();
            _slide = null;
            _velocity?.Recycle();
            _velocity = null;
            _dragging = _tracking = _settling = false;
            ScreenMotion.Interactive = false;
            ThemePalette.RefreshPlatform();
        }
    }

    public void Cancel()
    {
        _tracking = false;
        _cancelRequested = true;
        _slide?.Dispose();
        if (_dragging) _ = FinishAsync(false);
        ScreenMotion.Reset();
    }

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
