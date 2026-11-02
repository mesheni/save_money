using SaveMoney.Core.Services;

namespace SaveMoney.Tests;

public class InteractivePagingTests
{
    [Theory]
    [InlineData(-79, 320, 0, 1, false)]
    [InlineData(-80, 320, 0, 1, true)]
    [InlineData(80, 320, 0, -1, true)]
    [InlineData(-23, 411, -900, 1, false)]
    [InlineData(-24, 411, -450, 1, true)]
    [InlineData(24, 411, 450, -1, true)]
    [InlineData(-140, 411, 500, 1, false)]
    [InlineData(140, 411, -500, -1, false)]
    [InlineData(100, 411, 900, 1, false)]
    [InlineData(-100, 411, -900, -1, false)]
    [InlineData(100, 0, 900, -1, false)]
    [InlineData(100, 411, 900, 0, false)]
    public void DragCompletesOnlyTowardItsNeighbour(double distance, double width, double velocity, int direction, bool expected) =>
        Assert.Equal(expected, TabSwipePolicy.ShouldCommit(distance, width, velocity, direction));
}
