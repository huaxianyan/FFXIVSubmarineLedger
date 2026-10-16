namespace FFXIVSubLedger.Infrastructure.Recognition;

/// <summary>
/// 把格位切成归一化图标。
///
/// 三步：向内收缩掉格子边框 → 双线性缩放到固定边长 → 标出数量数字区域。
/// 收缩是必要的：格子边框在截图里宽度一致，但颜色随底下图标变化，
/// 留着会让同一道具在不同底色下比对不上。
/// </summary>
public static class IconExtractor
{
    public static IconPatch Extract(RgbImage image, GridCell cell, IconExtractionOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(cell);
        options ??= new IconExtractionOptions();

        int size = options.PatchSize;
        double insetX = cell.Width * options.InsetRatio;
        double insetY = cell.Height * options.InsetRatio;
        double originX = cell.X + insetX;
        double originY = cell.Y + insetY;
        double sourceWidth = cell.Width - (insetX * 2);
        double sourceHeight = cell.Height - (insetY * 2);

        var pixels = new byte[size * size * 3];
        var mask = new bool[size * size];

        for (int py = 0; py < size; py++)
        {
            for (int px = 0; px < size; px++)
            {
                // 按目标像素中心对齐到源图坐标，缩放的常见做法。
                double sourceX = originX + ((((px + 0.5) * sourceWidth) / size) - 0.5);
                double sourceY = originY + ((((py + 0.5) * sourceHeight) / size) - 0.5);

                int index = (py * size) + px;
                int offset = index * 3;

                for (int channel = 0; channel < 3; channel++)
                    pixels[offset + channel] = (byte)Math.Clamp(SampleChannel(image, sourceX, sourceY, channel), 0, 255);

                // 数字区按「相对整个格子」的比例判断，而不是相对收缩后的区域——
                // 收缩比例将来可能调整，数字的物理位置不该跟着变。
                double relativeX = (sourceX - cell.X) / cell.Width;
                double relativeY = (sourceY - cell.Y) / cell.Height;
                mask[index] = relativeX >= options.QuantityLeft && relativeY >= options.QuantityTop;
            }
        }

        return new IconPatch(size, pixels, mask);
    }

    /// <summary>双线性插值取一个通道的值。</summary>
    private static double SampleChannel(RgbImage image, double x, double y, int channel)
    {
        double clampedX = Math.Clamp(x, 0, image.Width - 1);
        double clampedY = Math.Clamp(y, 0, image.Height - 1);

        int x0 = (int)Math.Floor(clampedX);
        int y0 = (int)Math.Floor(clampedY);
        int x1 = Math.Min(x0 + 1, image.Width - 1);
        int y1 = Math.Min(y0 + 1, image.Height - 1);

        double fx = clampedX - x0;
        double fy = clampedY - y0;

        double top = (image.At(x0, y0, channel) * (1 - fx)) + (image.At(x1, y0, channel) * fx);
        double bottom = (image.At(x0, y1, channel) * (1 - fx)) + (image.At(x1, y1, channel) * fx);

        return (top * (1 - fy)) + (bottom * fy);
    }
}
