namespace SaveMoney.Controls;

/// <summary>
/// Полоска 2dp над активной вкладкой таб-бара (макет). Реализация — только на Android
/// (Platforms/Android/TabIndicatorEffect.cs); на остальных платформах эффект no-op.
/// Движение полоски отслеживает Shell.CurrentItem — стандартный listener MAUI не трогается.
/// </summary>
public class TabIndicatorEffect : RoutingEffect
{
    public TabIndicatorEffect() : base("SaveMoney.TabIndicatorEffect")
    {
    }
}
