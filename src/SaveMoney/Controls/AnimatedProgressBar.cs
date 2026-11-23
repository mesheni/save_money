namespace SaveMoney.Controls;

/// <summary>
/// ProgressBar, который плавно доезжает до нового значения (Android: Platforms/Android/MotionHooks).
/// При переиспользовании строки списка (смена BindingContext) значение ставится сразу —
/// иначе полоса «переезжала» бы от значения чужой строки.
/// </summary>
public class AnimatedProgressBar : ProgressBar
{
    private bool _snapNext;

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        _snapNext = Handler is not null;
    }

    internal bool ConsumeSnap()
    {
        var snap = _snapNext;
        _snapNext = false;
        return snap;
    }
}
