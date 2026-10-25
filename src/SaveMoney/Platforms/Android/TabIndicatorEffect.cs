using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Platform.Compatibility;

[assembly: ResolutionGroupName("SaveMoney")]
[assembly: ExportEffect(typeof(SaveMoney.Platforms.Android.TabIndicatorEffect), nameof(SaveMoney.Controls.TabIndicatorEffect))]

namespace SaveMoney.Platforms.Android;

/// <summary>
/// Полоска 2dp над активной вкладкой нижнего таб-бара Shell (эффект вешается на Shell
/// в AppShell.xaml). MAUI Shell рисует табы через BottomNavigationView, где нет
/// штатного индикатора вкладки: полоска — дочерний View, позиция двигается по
/// Shell.CurrentItem. Свой ItemSelected listener MAUI не перезаписывается.
/// </summary>
public class TabIndicatorEffect : PlatformEffect<global::Android.Views.ViewGroup, global::Android.Views.View>
{
    private BottomNavigationView? _navView;
    private global::Android.Views.View? _strip;
    private Shell? _shell;
    private bool _attached;

    protected override void OnAttached()
    {
        _shell = Element as Shell;
        if (_shell is null)
        {
            return;
        }

        _shell.PropertyChanged += OnShellPropertyChanged;
        _attached = true;

        // Таб-бар собирается лениво: после первой раскладки ищем BottomNavigationView.
        if (Container is global::Android.Views.ViewGroup group && group.ViewTreeObserver is not null)
        {
            var listener = new OnceLayoutListener(group.ViewTreeObserver, () => FindAndAttach(group));
            group.ViewTreeObserver.AddOnGlobalLayoutListener(listener);
        }
    }

    private void FindAndAttach(global::Android.Views.ViewGroup root)
    {
        _navView = FindBottomNavigationView(root);
        if (_navView is null || _strip is not null)
        {
            return;
        }

        var density = _navView.Resources?.DisplayMetrics?.Density ?? 1f;
        var accent = Application.Current?.RequestedTheme == AppTheme.Dark
            ? new global::Android.Graphics.Color(0xFF, 0x7A, 0x1A)
            : new global::Android.Graphics.Color(0xFF, 0x6B, 0x00);

        _strip = new global::Android.Views.View(_navView.Context!)
        {
            Background = new global::Android.Graphics.Drawables.ColorDrawable(accent),
        };
        var heightPx = (int)Math.Round(2 * density);
        // Ширина и X задаются в UpdateStrip; BottomNavigationView — FrameLayout.
        _strip.LayoutParameters = new global::Android.Widget.FrameLayout.LayoutParams(
            global::Android.Views.ViewGroup.LayoutParams.MatchParent, heightPx,
            global::Android.Views.GravityFlags.Top);

        _navView.AddView(_strip);
        UpdateStrip(animated: false);
    }

    private void OnShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Shell.CurrentItem) or nameof(Shell.CurrentState))
        {
            UpdateStrip(animated: true);
        }
    }

    private void UpdateStrip(bool animated)
    {
        if (_navView is null || _strip is null || _shell is null)
        {
            return;
        }

        var menuSize = _navView.Menu?.Size() ?? 0;
        if (menuSize == 0 || _navView.Width == 0)
        {
            return;
        }

        var index = _shell.CurrentItem is null ? 0 : _shell.Items.IndexOf(_shell.CurrentItem);
        if (index < 0)
        {
            index = 0;
        }

        var itemWidth = _navView.Width / menuSize;
        var targetX = index * itemWidth;

        if (animated && _strip.TranslationX != targetX)
        {
            _strip.Animate()?.SetDuration(180)
                .SetInterpolator(new global::Android.Views.Animations.DecelerateInterpolator())
                .TranslationX(targetX);
        }
        else
        {
            _strip.TranslationX = targetX;
        }

        // Ширина вкладки может измениться при повороте — перезадать параметр.
        if (_strip.LayoutParameters is global::Android.Widget.FrameLayout.LayoutParams lp && lp.Width != itemWidth)
        {
            lp.Width = itemWidth;
            _strip.LayoutParameters = lp;
        }
    }

    private static BottomNavigationView? FindBottomNavigationView(global::Android.Views.View root)
    {
        if (root is BottomNavigationView nav)
        {
            return nav;
        }

        if (root is global::Android.Views.ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                var found = FindBottomNavigationView(group.GetChildAt(i)!);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    protected override void OnDetached()
    {
        if (_attached && _shell is not null)
        {
            _shell.PropertyChanged -= OnShellPropertyChanged;
            _attached = false;
        }

        if (_strip?.Parent is global::Android.Views.ViewGroup parent)
        {
            parent.RemoveView(_strip);
        }
        _strip?.Dispose();
        _strip = null;
        _navView = null;
    }

    /// <summary>Одноразовый GlobalLayout: после первой раскладки ищем BottomNavigationView и отписываемся.</summary>
    private class OnceLayoutListener(global::Android.Views.ViewTreeObserver observer, Action action)
        : Java.Lang.Object, global::Android.Views.ViewTreeObserver.IOnGlobalLayoutListener
    {
        private bool _fired;

        public void OnGlobalLayout()
        {
            if (_fired)
            {
                return;
            }

            _fired = true;
            try
            {
                observer.RemoveOnGlobalLayoutListener(this);
            }
            catch
            {
                // observer мог быть мёртв к моменту отписки — не критично.
            }

            action();
        }
    }
}
