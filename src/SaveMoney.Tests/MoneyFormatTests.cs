using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

public class MoneyFormatTests
{
    [Theory]
    [InlineData("1 500,50", 150_050)]
    [InlineData("1500.5", 150_050)]
    [InlineData("123", 12_300)]
    [InlineData("1\u00a0250", 125_000)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    public void ParseInput_BasicCases(string input, long expected)
    {
        Assert.Equal(expected, MoneyFormat.ParseInput(input));
    }

    [Theory]
    [InlineData("100,125", 10_013)]
    [InlineData("1,005", 101)]
    [InlineData("0,005", 1)]
    public void ParseInput_ThirdDecimal_RoundsAwayFromZero(string input, long expected)
    {
        // Свободные поля (лимит бюджета, сумма долга) принимают 3+ знака после запятой;
        // банковское (к чётному) округление молча теряло копейку: 10012,5 → 10012.
        Assert.Equal(expected, MoneyFormat.ParseInput(input));
    }

    [Fact]
    public void ForInput_RoundTrips()
    {
        Assert.Equal("1550,05", MoneyFormat.ForInput(155_005));
        Assert.Equal("1500", MoneyFormat.ForInput(150_000));
        Assert.Equal("", MoneyFormat.ForInput(0));
    }
}
