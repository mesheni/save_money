namespace SaveMoney.Controls;

/// <summary>The selected background occupies the entire interactive segment on Android.</summary>
public sealed class SegmentButton : Button
{
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if ANDROID
        if (Handler?.PlatformView is Google.Android.Material.Button.MaterialButton native)
        {
            native.InsetTop = 0;
            native.InsetBottom = 0;
        }
#endif
    }
}
