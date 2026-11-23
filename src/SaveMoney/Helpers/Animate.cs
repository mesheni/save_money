namespace SaveMoney.Helpers;

/// <summary>
/// Подключаемые из XAML анимации:
/// <c>helpers:Animate.CountUp="True"</c> — сумма в Label «набегает» к новому значению
/// (Android, только отображение: привязка Text не трогается);
/// <c>helpers:Animate.Reveal="True"</c> — блок мягко проявляется, когда становится видимым.
/// </summary>
public static class Animate
{
    public static readonly BindableProperty CountUpProperty = BindableProperty.CreateAttached(
        "CountUp", typeof(bool), typeof(Animate), false);

    public static bool GetCountUp(BindableObject view) => (bool)view.GetValue(CountUpProperty);

    public static void SetCountUp(BindableObject view, bool value) => view.SetValue(CountUpProperty, value);

    public static readonly BindableProperty RevealProperty = BindableProperty.CreateAttached(
        "Reveal", typeof(bool), typeof(Animate), false, propertyChanged: OnRevealChanged);

    public static bool GetReveal(BindableObject view) => (bool)view.GetValue(RevealProperty);

    public static void SetReveal(BindableObject view, bool value) => view.SetValue(RevealProperty, value);

    private static void OnRevealChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is not VisualElement view) return;
        view.PropertyChanged -= OnRevealTargetChanged;
        if ((bool)newValue) view.PropertyChanged += OnRevealTargetChanged;
    }

    private static void OnRevealTargetChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        // Первое заполнение страницы не анимируем: проявляется только то, что
        // появилось на уже показанном экране (смена режима, ошибка, подсказка).
        if (args.PropertyName != nameof(VisualElement.IsVisible)
            || sender is not VisualElement { IsVisible: true, Handler: not null, Window: not null } view) return;
        _ = view.RevealAsync();
    }
}
