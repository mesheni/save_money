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
