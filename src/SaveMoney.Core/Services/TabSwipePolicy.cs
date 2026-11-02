namespace SaveMoney.Core.Services;

public static class TabSwipePolicy
{
    /// <summary>Complete a drag by distance or a deliberate fling toward the prepared neighbour.</summary>
    public static bool ShouldCommit(double horizontalDp, double widthDp, double velocityDpPerSecond, int direction)
    {
        if (widthDp <= 0 || direction is not (-1 or 1)) return false;
        var travel = -direction * horizontalDp;
        var velocity = -direction * velocityDpPerSecond;
        // A reversal at release cancels even after a long drag.
        if (velocity < -300) return false;
        return travel >= widthDp * .25 || travel >= 24 && velocity >= 450;
    }

    public static int GetStep(double horizontalDp, double maximumVerticalDp)
    {
        if (Math.Abs(horizontalDp) < 64 || Math.Abs(horizontalDp) < 2 * Math.Abs(maximumVerticalDp))
            return 0;
        return horizontalDp < 0 ? 1 : -1;
    }
}
