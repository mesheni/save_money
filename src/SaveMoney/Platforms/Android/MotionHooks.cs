using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Android.Animation;
using Android.Views.Animations;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Handlers;
using SaveMoney.Controls;
using SaveMoney.Helpers;
using AProgressBar = Android.Widget.ProgressBar;
using ATextView = Android.Widget.TextView;
using AView = Android.Views.View;

namespace SaveMoney.Platforms.Android;

/// <summary>
/// Нативные (RenderThread) анимации элементов, подключаемые через мапперы хендлеров:
/// отклик на нажатие, заполнение прогресса, перебор сумм и каскадное появление списков.
/// Всё уважает системный масштаб анимаций (<see cref="Motion.AnimationsEnabled"/>).
/// </summary>
public static class MotionHooks
{
    internal static readonly IInterpolator Standard = new PathInterpolator(0.2f, 0f, 0f, 1f);

    public static void Install()
    {
        ButtonHandler.Mapper.AppendToMapping("PressMotion", (_, view) =>
        {
            if (view is Button button) PressMotion.AttachButton(button);
        });
        ViewHandler.ViewMapper.AppendToMapping("TapMotion", (handler, view) =>
        {
            if (view is View element && handler.PlatformView is AView native) PressMotion.AttachTappable(element, native);
        });
        ProgressBarHandler.Mapper.ModifyMapping(nameof(IProgress.Progress), ProgressMotion.Map);
        LabelHandler.Mapper.ModifyMapping(nameof(ILabel.Text), CountMotion.Map);
        CollectionViewHandler.Mapper.ModifyMapping(nameof(ItemsView.ItemsSource), (handler, view, previous) =>
        {
            previous?.Invoke(handler, view);
            if (handler.PlatformView is RecyclerView list) ListMotion.Hook(list);
        });
    }
}

/// <summary>Пружинящее сжатие нажатого элемента: кнопки, карточки, строки списков.</summary>
internal static class PressMotion
{
    private static readonly ConditionalWeakTable<object, object> Attached = new();
    private static readonly IInterpolator Release = new OvershootInterpolator(2.4f);

    public static void AttachButton(Button button)
    {
        if (Attached.TryGetValue(button, out _)) return;
        Attached.Add(button, button);
        button.Pressed += (_, _) => Animate(Target(button), true);
        button.Released += (_, _) => Animate(Target(button), false);
    }

    public static void AttachTappable(View view, AView native)
    {
        if (view is Button || !view.GestureRecognizers.OfType<TapGestureRecognizer>().Any()
            || Attached.TryGetValue(native, out _)) return;
        Attached.Add(native, view);
        // MAUI подписывается тем же событием (Touch +=) и сам выставляет Handled — не трогаем его.
        native.Touch += (_, args) =>
        {
            switch (args.Event?.ActionMasked)
            {
                case global::Android.Views.MotionEventActions.Down:
                    Animate(native, true);
                    break;
                case global::Android.Views.MotionEventActions.Up:
                case global::Android.Views.MotionEventActions.Cancel:
                    Animate(native, false);
                    break;
            }
        };
        // Переиспользованная строка списка не должна остаться сжатой.
        native.ViewDetachedFromWindow += (_, _) => Reset(native);
    }

    /// <summary>Прозрачная кнопка-подложка поверх карточки сжимает саму карточку.</summary>
    private static AView? Target(Button button)
    {
        VisualElement target = button;
        if (string.IsNullOrEmpty(button.Text) && button.BackgroundColor is { Alpha: 0 })
        {
            Element? parent = button.Parent;
            for (var depth = 0; depth < 3 && parent is not null; depth++, parent = parent.Parent)
            {
                if (parent is Border border)
                {
                    target = border;
                    break;
                }
            }
        }

        return target.Handler is IViewHandler handler
            ? handler.ContainerView as AView ?? handler.PlatformView as AView
            : null;
    }

    private static void Animate(AView? native, bool down)
    {
        if (native is null || native.Handle == IntPtr.Zero || !Motion.AnimationsEnabled) return;
        var animator = native.Animate();
        if (animator is null) return;
        animator.Cancel();
        if (down)
        {
            var density = native.Resources?.DisplayMetrics?.Density ?? 1f;
            var widthDp = native.Width / density;
            // Крупное сжимается едва заметно, мелкое — выразительнее.
            var scale = widthDp > 240 ? 0.975f : widthDp > 120 ? 0.96f : 0.92f;
            animator.ScaleX(scale).ScaleY(scale).SetDuration(110).SetInterpolator(MotionHooks.Standard).Start();
        }
        else
        {
            animator.ScaleX(1f).ScaleY(1f).SetDuration(340).SetInterpolator(Release).Start();
        }
    }

    private static void Reset(AView native)
    {
        if (native.Handle == IntPtr.Zero) return;
        native.Animate()?.Cancel();
        native.ScaleX = 1f;
        native.ScaleY = 1f;
    }
}

/// <summary>Прогресс плавно доезжает до нового значения; первое значение — заполнение с нуля.</summary>
internal static class ProgressMotion
{
    private static readonly ConditionalWeakTable<AProgressBar, ObjectAnimator> Running = new();

    public static void Map(IProgressBarHandler handler, IProgress view, Action<IElementHandler, IElement>? previous)
    {
        if (handler.PlatformView is not AProgressBar native)
        {
            previous?.Invoke(handler, view);
            return;
        }

        if (Running.TryGetValue(native, out var running))
        {
            running.Cancel();
            Running.Remove(native);
        }

        var snap = view is not AnimatedProgressBar bar || bar.ConsumeSnap();
        var target = (int)Math.Round(Math.Clamp(view.Progress, 0, 1) * native.Max);
        var from = native.Progress;
        if (snap || from == target || !Motion.AnimationsEnabled)
        {
            previous?.Invoke(handler, view);
            return;
        }

        var animator = ObjectAnimator.OfInt(native, "progress", from, target)!;
        var share = Math.Abs(target - from) / (double)Math.Max(1, native.Max);
        animator.SetDuration((long)(Motion.Normal + (Motion.Emphasized - Motion.Normal) * share + 120));
        animator.SetInterpolator(MotionHooks.Standard);
        Running.AddOrUpdate(native, animator);
        animator.Start();
    }
}

/// <summary>
/// Перебор суммы в Label с <see cref="Helpers.Animate.CountUpProperty"/>: промежуточные
/// значения пишутся только в нативный TextView, Text и привязка не меняются.
/// </summary>
internal static class CountMotion
{
    private sealed class State
    {
        public string? Shown;
        public ValueAnimator? Animator;
        public int Generation;
    }

    private static readonly ConditionalWeakTable<Label, State> States = new();
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    private static readonly Regex Number = new(
        @"(?<sign>[+−\-]?)(?<int>\d{1,3}(?:[   ]\d{3})+|\d+)(?:,(?<frac>\d+))?",
        RegexOptions.CultureInvariant);

    public static void Map(ILabelHandler handler, ILabel view, Action<IElementHandler, IElement>? previous)
    {
        if (view is not Label label || !Helpers.Animate.GetCountUp(label) || handler.PlatformView is not ATextView native)
        {
            previous?.Invoke(handler, view);
            return;
        }

        // Моноширинные цифры: ширина суммы не «дрожит» во время перебора.
        native.FontFeatureSettings = "tnum";
        var state = States.GetOrCreateValue(label);
        var text = label.Text;
        if (state.Animator is not null && text == state.Shown) return;

        state.Generation++;
        state.Animator?.Cancel();
        state.Animator = null;
        var shownBefore = state.Shown;
        state.Shown = text;

        if (!Motion.AnimationsEnabled || !TryParse(text, out var target, out var format)
            || (shownBefore is null ? 0m : TryParse(shownBefore, out var parsed, out _) ? parsed : (decimal?)null) is not { } from
            || from == target
            // Промежуточное значение не должно быть длиннее итогового (обрезка в Auto-колонках).
            || Digits(from) > Digits(target))
        {
            previous?.Invoke(handler, view);
            return;
        }

        var generation = state.Generation;
        var animator = ValueAnimator.OfFloat(0f, 1f)!;
        animator.SetDuration(Motion.Count);
        animator.SetInterpolator(MotionHooks.Standard);
        animator.Update += (_, args) =>
        {
            if (generation != state.Generation || native.Handle == IntPtr.Zero) return;
            var t = (decimal)((Java.Lang.Float)args.Animation.AnimatedValue!).FloatValue();
            native.Text = format(from + (target - from) * t);
        };
        animator.AnimationEnd += (_, _) =>
        {
            if (generation != state.Generation) return;
            state.Animator = null;
            previous?.Invoke(handler, view);
        };
        state.Animator = animator;
        animator.Start();
    }

    private static int Digits(decimal value) => decimal.Truncate(Math.Abs(value)).ToString(CultureInfo.InvariantCulture).Length;

    /// <summary>Разбирает первую сумму в тексте и возвращает форматтер в том же оформлении.</summary>
    private static bool TryParse(string? text, out decimal value, out Func<decimal, string> format)
    {
        value = 0;
        format = _ => text ?? string.Empty;
        if (string.IsNullOrEmpty(text)) return false;
        var match = Number.Match(text);
        if (!match.Success) return false;

        var integer = match.Groups["int"].Value;
        var fraction = match.Groups["frac"].Value;
        if (!decimal.TryParse(new string(integer.Where(char.IsDigit).ToArray()) + (fraction.Length > 0 ? "." + fraction : ""),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) return false;

        var sign = match.Groups["sign"].Value;
        if (sign is "−" or "-") value = -value;

        var prefix = text[..match.Index];
        var suffix = text[(match.Index + match.Length)..];
        var plus = sign == "+";
        var minus = sign == "-" ? "-" : MoneyMinus;
        var separator = integer.FirstOrDefault(c => !char.IsDigit(c));
        var numberFormat = (NumberFormatInfo)Russian.NumberFormat.Clone();
        numberFormat.NumberDecimalSeparator = ",";
        numberFormat.NumberGroupSeparator = separator == default ? Russian.NumberFormat.NumberGroupSeparator : separator.ToString();
        var pattern = "N" + fraction.Length.ToString(CultureInfo.InvariantCulture);
        format = number =>
        {
            var rounded = Math.Round(number, fraction.Length);
            var signText = rounded < 0 ? minus : rounded > 0 && plus ? "+" : string.Empty;
            return prefix + signText + Math.Abs(rounded).ToString(pattern, numberFormat) + suffix;
        };
        return true;
    }

    private const string MoneyMinus = "−";
}

/// <summary>Каскадное появление строк при первом заполнении списка.</summary>
internal static class ListMotion
{
    private static readonly ConditionalWeakTable<RecyclerView, object> Done = new();

    public static void Hook(RecyclerView list)
    {
        if (Done.TryGetValue(list, out _) || list.GetAdapter() is not { } adapter) return;
        if (adapter.ItemCount > 0)
        {
            Run(list);
            return;
        }

        var observer = new FirstItems(list, adapter);
        adapter.RegisterAdapterDataObserver(observer);
    }

    private static void Run(RecyclerView list)
    {
        if (Done.TryGetValue(list, out _)) return;
        Done.Add(list, list);
        if (!Motion.AnimationsEnabled) return;

        var density = list.Resources?.DisplayMetrics?.Density ?? 1f;
        var item = new AnimationSet(true) { Duration = 360, Interpolator = MotionHooks.Standard };
        item.AddAnimation(new AlphaAnimation(0f, 1f));
        item.AddAnimation(new TranslateAnimation(0f, 0f, 28f * density, 0f));
        list.LayoutAnimation = new LayoutAnimationController(item, 0.1f);
        list.ScheduleLayoutAnimation();
        // Только один раз: дальнейшие перерисовки списка идут без каскада.
        list.PostDelayed(() =>
        {
            if (list.Handle != IntPtr.Zero) list.LayoutAnimation = null;
        }, 2000);
    }

    private sealed class FirstItems(RecyclerView list, RecyclerView.Adapter adapter) : RecyclerView.AdapterDataObserver
    {
        public override void OnChanged() => Check();

        public override void OnItemRangeInserted(int positionStart, int itemCount) => Check();

        private void Check()
        {
            if (adapter.ItemCount == 0) return;
            adapter.UnregisterAdapterDataObserver(this);
            if (list.Handle != IntPtr.Zero && ReferenceEquals(list.GetAdapter(), adapter)) Run(list);
        }
    }
}
