using Microsoft.Maui.Controls;
using SaveMoney.Helpers;

namespace SaveMoney.Controls;

/// <summary>
/// ProgressBar, который при первой установке значения проигрывает заполнение
/// (350 мс, кривая Vibrant) вместо мгновенного скачка. Для строк списков
/// (бюджеты, долги, топ категорий): при перезаполнении BindingContext эффект
/// повторяется, поэтому используется только в коротких списках.
/// </summary>
public class AnimatedProgressBar : ProgressBar
{
    private bool _animated;

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        _animated = false;
    }

    protected override void OnPropertyChanged(string? propertyName)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == ProgressProperty.PropertyName && !_animated && Progress > 0)
        {
            _animated = true;
            var target = Progress;
            Progress = 0;
            _ = this.ProgressTo(target, 350, Motion.Vibrant);
        }
    }
}
