using Google.Android.Material.BottomNavigation;

namespace SaveMoney.Platforms.Android;

/// <summary>
/// Полоска 2dp над активной вкладкой нижнего таб-бара Shell (макет).
/// Вешается напрямую на HandlerChanged/PropertyChanged Shell — БЕЗ механизма
/// Effects: EffectsFactory в DI MAUI 10 не зарегистрирован и крашит приложение
/// при attach (на WinUI это доказал смоук, на Android — та же причина «не запускается»).
/// MAUI Shell рисует табы через BottomNavigationView без штатного индикатора вкладки,
/// поэтому полоска — дочерний View, позиция движется по Shell.CurrentItem.
/// Свой ItemSelected listener MAUI не перезаписывается. Любой сбой глушится:
/// полоска — косметика и не должна валить приложение.
/// </summary>
public static class TabBarIndicator
{
    private static BottomNavigationView? _navView;
    private static global::Android.Views.View? _strip;
    private static Shell? _shell;
    private static bool _subscribed;

    public static void Attach(Shell shell)
    {
        _shell = shell;
        if (!_subscribed)
        {
            shell.HandlerChanged += OnHandlerChanged;
            shell.PropertyChanged += OnShellPropertyChanged;
            _subscribed = true;
        }

        if (shell.Handler is not null)
        {
            TryFindAndAttach(shell);
        }
    }

    private static void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Shell shell && shell.Handler is not null)
        {
            TryFindAndAttach(shell);
        }
    }

    private static void TryFindAndAttach(Shell shell)
    {
        try
        {
            if (_strip is not null)
            {
                UpdateStrip(animated: false);
                return;
            }

            if (shell.Handler?.PlatformView is not global::Android.Views.ViewGroup root)
            {
                return;
            }

            // Таб-бар собирается лениво: после первой раскладки ищем BottomNavigationView.
            if (root.ViewTreeObserver is not null)
            {
                root.ViewTreeObserver.AddOnGlobalLayoutListener(new OnceLayoutListener(root.ViewTreeObserver,
                    () =>
                    {
                        try
                        {
                            FindAndAttach(root);
                        }
                        catch
                        {
                            // полоска не должна валить приложение
                        }
                    }));
            }
        }
        catch
        {
            // полоска не должна валить приложение
        }
    }

    private static void FindAndAttach(global::Android.Views.ViewGroup root)
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

    private static void OnShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Shell.CurrentItem) or nameof(Shell.CurrentState))
        {
            try
            {
                UpdateStrip(animated: true);
            }
            catch
            {
                // полоска не должна валить приложение
            }
        }
    }

    private static void UpdateStrip(bool animated)
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
