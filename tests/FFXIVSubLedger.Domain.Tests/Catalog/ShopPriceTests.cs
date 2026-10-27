using FFXIVSubLedger.Domain.Catalog;

namespace FFXIVSubLedger.Domain.Tests.Catalog;

public sealed class ShopPriceTests
{
    [Theory]
    // 这五组是拿真实道具的 NQ/HQ 双价反推出来的，用来锁住「向上取整」这个方向。
    [InlineData(2, 3)]
    [InlineData(5, 6)]
    [InlineData(12, 14)]
    [InlineData(36, 40)]
    [InlineData(34500, 37950)]
    public void For_RoundsHighQualityUp(int nq, int expected)
    {
        Assert.Equal(expected, ShopPrice.For(nq, highQuality: true));
    }

    [Fact]
    public void For_ReturnsNqPriceForNormalQuality()
    {
        Assert.Equal(34500, ShopPrice.For(34500, highQuality: false));
    }

    [Fact]
    public void For_HandlesZeroWithoutRounding()
    {
        Assert.Equal(0, ShopPrice.For(0, highQuality: true));
    }
}
