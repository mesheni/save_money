using Microsoft.Maui.Controls;

namespace SaveMoney.Helpers;

/// <summary>Моушн-токены дизайн-контракта Vibrant: 150/240 мс, кривая cubic-bezier(0.2,0,0,1).</summary>
public static class Motion
{
    /// <summary>Honors Android's system-wide animator scale when the user disables motion.</summary>
    public static bool AnimationsEnabled
    {
        get
        {
#if ANDROID
            try
            {
                return Android.Provider.Settings.Global.GetFloat(
                    Android.App.Application.Context.ContentResolver!,
                    Android.Provider.Settings.Global.AnimatorDurationScale, 1f) > 0f;
            }
            catch (Exception)
            {
                return true;
            }
#else
            return true;
#endif
        }
    }

    /// <summary>Микро-взаимодействия (отклик на нажатие), мс.</summary>
    public const uint Micro = 150;

    /// <summary>Появления и переходы, мс.</summary>
    public const uint Normal = 240;

    /// <summary>Выразительные изменения: заполнение прогресса, диаграммы, мс.</summary>
    public const uint Emphasized = 450;

    /// <summary>Перебор суммы до нового значения, мс.</summary>
    public const uint Count = 650;

    /// <summary>Та же кривая как чистая функция — для LiveCharts (Func&lt;float,float&gt;).</summary>
    public static readonly Func<double, double> VibrantFunc = Bezier(0.2, 0, 0, 1);

    /// <summary>Кривая из дизайн-контракта: cubic-bezier(0.2, 0, 0, 1).</summary>
    public static readonly Easing Vibrant = new(VibrantFunc);

    /// <summary>
    /// Функция easing кубической Безье с опорными точками (0,0) и (1,1).
    /// Вход t — прогресс по X; выход — значение Y. X(u) монотонен, поэтому
    /// уравнение X(u)=t решаем бисекцией.
    /// </summary>
    private static Func<double, double> Bezier(double x1, double y1, double x2, double y2) => t =>
    {
        if (t <= 0)
        {
            return 0;
        }

        if (t >= 1)
        {
            return 1;
        }

        double X(double u) => 3 * u * (1 - u) * (1 - u) * x1 + 3 * u * u * (1 - u) * x2 + u * u * u;
        double Y(double u) => 3 * u * (1 - u) * (1 - u) * y1 + 3 * u * u * (1 - u) * y2 + u * u * u;

        var lo = 0.0;
        var hi = 1.0;
        for (var i = 0; i < 24; i++)
        {
            var mid = (lo + hi) / 2;
            if (X(mid) < t)
            {
                lo = mid;
            }
            else
            {
                hi = mid;
            }
        }

        return Y((lo + hi) / 2);
    };
}

/// <summary>Короткие анимации элементов поверх токенов <see cref="Motion"/>.</summary>
public static class MotionExtensions
{
    /// <summary>Появление: прозрачность, подъём и лёгкое увеличение с пружиной.</summary>
    public static async Task PopInAsync(this VisualElement view, double rise = 16, double fromScale = 0.92)
    {
        view.AbortAnimation(nameof(PopInAsync));
        if (!Motion.AnimationsEnabled)
        {
            view.Opacity = 1;
            view.TranslationY = 0;
            view.Scale = 1;
            return;
        }

        view.Opacity = 0;
        view.TranslationY = rise;
        view.Scale = fromScale;
        var completion = new TaskCompletionSource();
        new Animation
        {
            { 0, 0.6, new Animation(v => view.Opacity = v, 0, 1, Motion.Vibrant) },
            { 0, 1, new Animation(v => view.TranslationY = v, rise, 0, Motion.Vibrant) },
            { 0, 1, new Animation(v => view.Scale = v, fromScale, 1, Easing.SpringOut) },
        }.Commit(view, nameof(PopInAsync), length: 380, finished: (_, _) => completion.TrySetResult());
        await completion.Task;
    }

    /// <summary>Исчезновение: прозрачность и уход вниз.</summary>
    public static async Task PopOutAsync(this VisualElement view, double drop = 12)
    {
        view.AbortAnimation(nameof(PopOutAsync));
        if (!Motion.AnimationsEnabled)
        {
            view.Opacity = 0;
            return;
        }

        var completion = new TaskCompletionSource();
        var start = view.Opacity;
        new Animation
        {
            { 0, 1, new Animation(v => view.Opacity = v, start, 0, Easing.CubicIn) },
            { 0, 1, new Animation(v => view.TranslationY = v, 0, drop, Easing.CubicIn) },
        }.Commit(view, nameof(PopOutAsync), length: Motion.Normal, finished: (_, _) => completion.TrySetResult());
        await completion.Task;
    }

    /// <summary>Мягкое проявление снизу — для блоков, которые показываются по ходу работы.</summary>
    public static Task RevealAsync(this VisualElement view) => view.PopInAsync(rise: 10, fromScale: 1);

    /// <summary>Затухающее покачивание по горизонтали — «так нельзя».</summary>
    public static async Task ShakeAsync(this VisualElement view, double amplitude = 10)
    {
        view.AbortAnimation(nameof(ShakeAsync));
        if (!Motion.AnimationsEnabled)
        {
            view.TranslationX = 0;
            return;
        }

        var completion = new TaskCompletionSource();
        new Animation(t => view.TranslationX = amplitude * Math.Sin(t * Math.PI * 6) * (1 - t))
            .Commit(view, nameof(ShakeAsync), length: 420, easing: Easing.Linear,
                finished: (_, _) => { view.TranslationX = 0; completion.TrySetResult(); });
        await completion.Task;
    }

    /// <summary>Акцент на значении: короткий «вдох» до 1.06 и возврат с пружиной.</summary>
    public static async Task PulseAsync(this VisualElement view, double peak = 1.06)
    {
        view.AbortAnimation(nameof(PulseAsync));
        if (!Motion.AnimationsEnabled)
        {
            view.Scale = 1;
            return;
        }

        var completion = new TaskCompletionSource();
        new Animation
        {
            { 0, 0.35, new Animation(v => view.Scale = v, 1, peak, Easing.CubicOut) },
            { 0.35, 1, new Animation(v => view.Scale = v, peak, 1, Easing.SpringOut) },
        }.Commit(view, nameof(PulseAsync), length: 420, finished: (_, _) => { view.Scale = 1; completion.TrySetResult(); });
        await completion.Task;
    }
}
