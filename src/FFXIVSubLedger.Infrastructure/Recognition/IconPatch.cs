namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 归一化后的图标特征：固定边长的 RGB 缩略图 + 屏蔽掩码。
///
/// 掩码标出的是数量数字所在的像素，比对时两边都跳过，
/// 否则「27」和「111」会因为字形差异被算成不同道具。
/// </summary>
public sealed class IconPatch
{
    /// <summary>相似度计算的原点。取中灰，而不是每张图各自的均值——见 <see cref="SimilarityTo"/>。</summary>
    private const double MidGray = 128;

    public IconPatch(int size, byte[] pixels, bool[] mask)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentNullException.ThrowIfNull(mask);

        if (pixels.Length != size * size * 3)
            throw new ArgumentException($"像素数据长度应为 {size * size * 3}，实际 {pixels.Length}。", nameof(pixels));

        if (mask.Length != size * size)
            throw new ArgumentException($"掩码长度应为 {size * size}，实际 {mask.Length}。", nameof(mask));

        Size = size;
        Pixels = pixels;
        Mask = mask;
    }

    public int Size { get; }

    /// <summary>逐像素排列，每像素 3 字节：R、G、B。</summary>
    public byte[] Pixels { get; }

    /// <summary>true 表示这个像素不参与比对（数量数字区域）。</summary>
    public bool[] Mask { get; }

    /// <summary>有效（未屏蔽）像素占的比例，用来判断这张图是不是几乎全是数字。</summary>
    public double EffectiveRatio
    {
        get
        {
            int valid = 0;
            foreach (bool masked in Mask)
            {
                if (!masked)
                    valid++;
            }

            return (double)valid / Mask.Length;
        }
    }

    /// <summary>
    /// 与另一张图标算相似度，范围 -1..1，1 表示完全一致。
    ///
    /// 分数是**结构项与颜色项的平均**，因为单看任何一项都会被骗：
    /// <list type="bullet">
    /// <item><b>结构项</b>：三个通道各自零均值化后算相关再平均。只看图案形状，
    /// 对亮度变化和整体颜色都不敏感。问题在于图案相近的两个道具会拿到高分——
    /// 实测钻石与绿宝石的结构项能到 0.5 以上。</item>
    /// <item><b>颜色项</b>：以中灰（128）为原点算余弦。保留「这是什么颜色」，
    /// 对整体亮度的等比缩放不敏感。问题在于同色系的不同道具会混淆。</item>
    /// </list>
    ///
    /// 两项合起来才分得开：颜色不同的道具被颜色项拉开，图案不同的道具被结构项拉开。
    /// 只统计双方都未被屏蔽的像素，所以数量数字（27 / 111）不参与比对。
    /// </summary>
    public double SimilarityTo(IconPatch other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (other.Size != Size)
            throw new ArgumentException($"尺寸不一致：{Size} 与 {other.Size}。", nameof(other));

        return (StructureScore(other) + ColourScore(other)) / 2;
    }

    /// <summary>图案形状的相似度：每个通道单独零均值化后算相关，再取平均。</summary>
    private double StructureScore(IconPatch other)
    {
        double total = 0;
        int channels = 0;

        for (int channel = 0; channel < 3; channel++)
        {
            double sumA = 0;
            double sumB = 0;
            int count = 0;

            for (int i = 0; i < Mask.Length; i++)
            {
                if (Mask[i] || other.Mask[i])
                    continue;

                sumA += Pixels[(i * 3) + channel];
                sumB += other.Pixels[(i * 3) + channel];
                count++;
            }

            if (count == 0)
                continue;

            double meanA = sumA / count;
            double meanB = sumB / count;
            double dot = 0;
            double normA = 0;
            double normB = 0;

            for (int i = 0; i < Mask.Length; i++)
            {
                if (Mask[i] || other.Mask[i])
                    continue;

                double a = Pixels[(i * 3) + channel] - meanA;
                double b = other.Pixels[(i * 3) + channel] - meanB;
                dot += a * b;
                normA += a * a;
                normB += b * b;
            }

            double denominator = Math.Sqrt(normA * normB);
            total += denominator <= double.Epsilon ? 0 : dot / denominator;
            channels++;
        }

        return channels == 0 ? 0 : total / channels;
    }

    /// <summary>
    /// 整体颜色的相似度：以中灰为原点算余弦。
    /// 减的是固定值而不是各自的均值，颜色方向才留得住——这正是不能用 ZNCC 的原因。
    /// </summary>
    private double ColourScore(IconPatch other)
    {
        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < Mask.Length; i++)
        {
            if (Mask[i] || other.Mask[i])
                continue;

            int offset = i * 3;
            for (int channel = 0; channel < 3; channel++)
            {
                double a = Pixels[offset + channel] - MidGray;
                double b = other.Pixels[offset + channel] - MidGray;
                dot += a * b;
                normA += a * a;
                normB += b * b;
            }
        }

        double denominator = Math.Sqrt(normA * normB);
        return denominator <= double.Epsilon ? 0 : dot / denominator;
    }
}
