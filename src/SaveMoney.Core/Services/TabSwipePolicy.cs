namespace SaveMoney.Core.Services;

public static class TabSwipePolicy
{
    public static int GetStep(double horizontalDp, double maximumVerticalDp)
    {
        if (Math.Abs(horizontalDp) < 64 || Math.Abs(horizontalDp) < 2 * Math.Abs(maximumVerticalDp))
            return 0;
        return horizontalDp < 0 ? 1 : -1;
    }
}
