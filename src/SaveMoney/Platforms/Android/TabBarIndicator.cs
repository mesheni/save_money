using Google.Android.Material.BottomNavigation;

namespace SaveMoney.Platforms.Android;

/// <summary>
/// Полоска 2dp над активной вкладкой нижнего таб-бара Shell (макет).
/// MAUI Shell рисует табы через BottomNavigationView без штатного индикатора вкладки,
/// поэтому полоска — дочерний View. Позиция синхронизируется лёгким опросом
/// (Handler, 200 мс): SelectedItemId таб-бара — истина в последней инстанции.
/// Прошлые попытки были ненадёжны: Shell.PropertyChanged не ловил переключение,
/// а ViewTreeObserver корня отмирает после первой раскладки (listener перестаёт
/// получать события). Механизм Effects не используется: EffectsFactory в DI MAUI 10
/// не зарегистрирован и валил приложение при attach. Любой сбой глушится:
/// полоска — косметика и не должна валить приложение.
/// </summary>
public static class TabBarIndicator
{
    private static bool _subscribed;
    private static bool _started;

    public static void Attach(Shell shell)
    {
        try
        {
            if (!_subscribed)
            {
                // Attach вызывается из конструктора AppShell — Handler ещё не создан.
                // Стартуем цикл опроса при первом появлении PlatformView.
                _subscribed = true;
                shell.HandlerChanged += OnHandlerChanged;
            }

            TryStart(shell);
        }
        catch
        {
            // полоска не должна валить приложение
        }
    }

    private static void OnHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Shell shell)
        {
            TryStart(shell);
        }
    }

    private static void TryStart(Shell shell)
    {
        try
        {
            if (_started || shell.Handler?.PlatformView is not global::Android.Views.ViewGroup root)
            {
                return;
            }

            _started = true;
            var handler = new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!);

            // Первый проход сразу, затем каждые 200 мс: пара сравнений int —
            // нагрузка ничтожна, и полоска всегда на месте.
            handler.PostDelayed(() => Tick(handler, root), 100);
        }
        catch
        {
            // полоска не должна валить приложение
        }

        static void Tick(global::Android.OS.Handler handler, global::Android.Views.ViewGroup root)
        {
            try
            {
                Sync(root);
            }
            catch
            {
                // полоска не должна валить приложение
            }

            handler.PostDelayed(() => Tick(handler, root), 200);
        }
    }

    private static BottomNavigationView? _navView;
    private static global::Android.Views.View? _strip;
    private static int _lastIndex = -1;
    private static int _lastWidth = -1;
    private static global::Android.Graphics.Color? _lastAccent;

    private static void Sync(global::Android.Views.ViewGroup root)
    {
        if (_strip is null)
        {
            _navView = FindBottomNavigationView(root);
            if (_navView is null)
            {
                return;
            }

            var density = _navView.Resources?.DisplayMetrics?.Density ?? 1f;
            var accent = CurrentAccent();

            _strip = new global::Android.Views.View(_navView.Context!)
            {
                Background = new global::Android.Graphics.Drawables.ColorDrawable(accent),
            };
            _strip.LayoutParameters = new global::Android.Widget.FrameLayout.LayoutParams(
                0, (int)Math.Round(2 * density), global::Android.Views.GravityFlags.Top);
            _navView.AddView(_strip);
        }

        if (_navView is null || _strip is null)
        {
            return;
        }

        var currentAccent = CurrentAccent();
        if (_lastAccent != currentAccent)
        {
            _strip.SetBackgroundColor(currentAccent);
            _lastAccent = currentAccent;
        }

        var menuSize = _navView.Menu?.Size() ?? 0;
        if (menuSize == 0 || _navView.Width == 0)
        {
            return;
        }

        // Индекс активной вкладки берём у самого таб-бара — истина в последней инстанции.
        var selectedId = _navView.SelectedItemId;
        var index = 0;
        for (var i = 0; i < menuSize; i++)
        {
            if (_navView.Menu!.GetItem(i).ItemId == selectedId)
            {
                index = i;
                break;
            }
        }

        var itemWidth = _navView.Width / menuSize;
        if (itemWidth <= 0)
        {
            return;
        }

        var targetX = index * itemWidth;

        // Позицию и ширину задаём ТОЛЬКО при реальном изменении — иначе каждый
        // проход вызывает requestLayout по кругу.
        if (_lastIndex != index || _lastWidth != itemWidth)
        {
            _lastIndex = index;
            _lastWidth = itemWidth;
            if (_strip.LayoutParameters is global::Android.Widget.FrameLayout.LayoutParams lp)
            {
                lp.Width = itemWidth;
                _strip.LayoutParameters = lp;
            }

            _strip.TranslationX = targetX;
        }
        else if (_strip.TranslationX != targetX)
        {
            _strip.TranslationX = targetX;
        }
    }

    private static global::Android.Graphics.Color CurrentAccent()
    {
        var color = SaveMoney.Helpers.ThemePalette.AccentColor;
        return new global::Android.Graphics.Color((byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));
    }

    private static BottomNavigationView? FindBottomNavigationView(global::Android.Views.View view)
    {
        if (view is BottomNavigationView nav)
        {
            return nav;
        }

        if (view is global::Android.Views.ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                var child = group.GetChildAt(i);
                if (child is null)
                {
                    continue;
                }

                var found = FindBottomNavigationView(child);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }
}
