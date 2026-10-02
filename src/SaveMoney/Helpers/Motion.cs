using Microsoft.Maui.Controls;

namespace SaveMoney.Helpers;

/// <summary>Моушн-токены дизайн-контракта Vibrant: 150/240 мс, кривая cubic-bezier(0.2,0,0,1).</summary>
public static class Motion
{
    /// <summary>Микро-взаимодействия (отклик на нажатие), мс.</summary>
    public const uint Micro = 150;

    /// <summary>Появления и переходы, мс.</summary>
    public const uint Normal = 240;

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

public static class MotionExtensions
{
    /// <summary>Мягкое появление снизу: fade + подъём на 14px.</summary>
    public static async Task FadeInUpAsync(this View view, uint ms = Motion.Normal, int delayMs = 0)
    {
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }

        view.Opacity = 0;
        view.TranslationY = 14;
        await Task.WhenAll(
            view.FadeTo(1, ms, Motion.Vibrant),
            view.TranslateTo(0, 0, ms, Motion.Vibrant));
    }

    /// <summary>Каскадное появление: соседние элементы стартуют с шагом staggerMs.</summary>
    public static Task FadeInUpStaggeredAsync(this IEnumerable<View> views, int staggerMs = 30, uint ms = Motion.Normal) =>
        Task.WhenAll(views.Select((v, i) => v.FadeInUpAsync(ms, i * staggerMs)));

    /// <summary>Отклик на нажатие: сжатие до 0.96 и упругий возврат.</summary>
    public static async Task PulseAsync(this View view)
    {
        await view.ScaleTo(0.96, Motion.Micro, Easing.Linear);
        await view.ScaleTo(1, Motion.Micro + 90, Motion.Vibrant);
    }

    /// <summary>
    /// Счётчик: перебирает значение от from до to, прогоняя каждый кадр через apply.
    /// Важно: apply должен писать в свойство VM, а не в Label.Text напрямую —
    /// прямой set у привязанного Label стирает one-way binding, и метка
    /// перестаёт обновляться до пересоздания страницы.
    /// </summary>
    public static Task CountUpAsync(this Label ticker, double from, double to, Action<double> apply, uint ms = 500)
    {
        var tcs = new TaskCompletionSource();
        apply(from);
        new Animation(v => apply(from + (to - from) * v))
            .Commit(ticker, "CountUp", length: ms, easing: Motion.Vibrant, finished: (_, _) => tcs.TrySetResult());
        return tcs.Task;
    }
}
