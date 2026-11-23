namespace FFXIVSubmarineLedger.Domain.Texts;

/// <summary>
/// 两个字串差多远。物名和艇名都会遇到「OCR 读歪一两个字」的问题，共用同一个编辑距离，
/// 免得两边各写一份、阈值各调各的。
/// </summary>
public static class TextDistance
{
    /// <summary>编辑距离（插入、删除、替换各计 1）。</summary>
    public static int Levenshtein(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Length == 0)
            return right.Length;

        if (right.Length == 0)
            return left.Length;

        // 名字只有几十条、每条一二十字，直接用数组，不玩缓冲区复用。
        int[] previous = new int[right.Length + 1];
        int[] current = new int[right.Length + 1];

        for (int j = 0; j <= right.Length; j++)
            previous[j] = j;

        for (int i = 1; i <= left.Length; i++)
        {
            current[0] = i;

            for (int j = 1; j <= right.Length; j++)
            {
                int cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }
}
