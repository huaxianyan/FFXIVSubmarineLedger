namespace FFXIVSubmarineLedger.Domain.Catalog;

/// <summary>
/// 卖店价换算。游戏里 HQ 的买取价固定比 NQ 高一成，且**向上取整**。
///
/// 取整方向是用真实数据核出来的：NQ 2 → HQ 3、NQ 12 → HQ 14，四舍五入会得到 2 和 13，
/// 只有向上取整与全部样本吻合。写成整数运算，避免浮点误差带来 1 gil 偏差。
/// </summary>
public static class ShopPrice
{
    public static int For(int shopPriceNq, bool highQuality)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(shopPriceNq);

        return highQuality ? ((shopPriceNq * 11) + 9) / 10 : shopPriceNq;
    }
}
