using System.Globalization;

namespace FFXIVSubLedger.Infrastructure.Parsing;

/// <summary>
/// 从一条日文入手日志行里取出的原始信息。物名还是 OCR 那个串，没跟词表比对过。
/// </summary>
/// <param name="ItemText">物名原文，已去掉角色名、链接标记与数量。</param>
/// <param name="Quantity">入手数量。</param>
/// <param name="LooksHighQuality">数量前面多出来的标记，可能是 HQ 的 <c>◉</c>。词表说该物不能 HQ 时以词表为准。</param>
/// <param name="RawLine">原始行，留着给确认界面显示与排查用。</param>
public sealed record LootLine(string ItemText, int Quantity, bool LooksHighQuality, string RawLine);

/// <summary>
/// 日文客户端日志行解析。
///
/// 真实行形如 <c>[11:40] 角色名は ▶ 沈没船の高級首飾り×5を入手した。</c>。
/// OCR 会把这些符号读错，所以解析不认固定符号，只认结构：以「を入手」结尾、
/// 末尾是数字、数字前面是噪声字符、再前面是「は」加链接标记。
/// </summary>
public static class JapaneseLogLineParser
{
    private const string LootAnchor = "を入手";

    /// <summary>乘号被 OCR 读成的各种样子。</summary>
    private const string TimesNoise = "×XxｘⅩ*＊・･※";

    /// <summary>
    /// HQ 标记 <c>◉</c> 被读成的样子。只在「物名后、数量前」这一小段上剥，
    /// 且只剥一层——如果物名自己以这些假名结尾，会被削掉一个字，交给词表纠错嗂回去
    /// （当前词表里没有这种物名）。
    /// </summary>
    private const string HighQualityNoise = "◉⦿ぐでゅ";

    /// <summary>物名前面的物品链接标记。</summary>
    private const string LinkMarkers = "▶►▸▷pb";

    /// <summary>角色名最多这么长，再往后出现的「は」属于物名。</summary>
    private const int ActorNameLimit = 40;

    private const string SubmarinePrefix = "潜水艦「";
    private const string SubmarineReturnMarker = "」の探索完了";

    public static bool TryParse(string line, out LootLine parsed)
    {
        parsed = null!;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        string text = StripWhitespace(line);
        int anchor = text.IndexOf(LootAnchor, StringComparison.Ordinal);
        if (anchor <= 0)
            return false;

        string head = text[..anchor];

        // 数量：末尾的连续数字。
        int digitStart = head.Length;
        while (digitStart > 0 && char.IsAsciiDigit(head[digitStart - 1]))
            digitStart--;

        if (digitStart == head.Length)
            return false;

        if (!int.TryParse(head[digitStart..], NumberStyles.None, CultureInfo.InvariantCulture, out int quantity)
            || quantity <= 0)
        {
            return false;
        }

        string rest = head[..digitStart];
        bool looksHighQuality = false;

        // 数字与物名之间夹着的都是噪声：乘号类直接丢，HQ 标记类记成疑似 HQ。
        while (rest.Length > 0)
        {
            char last = rest[^1];

            if (TimesNoise.Contains(last, StringComparison.Ordinal))
            {
                rest = rest[..^1];
                continue;
            }

            if (HighQualityNoise.Contains(last, StringComparison.Ordinal))
            {
                looksHighQuality = true;
                rest = rest[..^1];
                continue;
            }

            break;
        }

        // 必须能找到「<角色名>は」这个分隔：真实聊天记录里还有「〜の景品として、…を入手しました。」
        // 这类没有主语的行，它们不是收艇收益，不能当成入手记录。
        if (!TryDropActorPrefix(rest, out rest))
            return false;

        rest = rest.TrimStart(LinkMarkers.ToCharArray());

        if (rest.Length == 0)
            return false;

        parsed = new LootLine(rest, quantity, looksHighQuality, line);
        return true;
    }

    /// <summary>
    /// 认出「潜水艦「某艇」の探索完了を確認しました。」这一行，返回艇名。
    ///
    /// 日志里艇名出现在它那批收益行**之后**，所以归属要倒着算：一行「探索完了」
    /// 结算它前面攒着的那批入手行。
    /// </summary>
    public static bool TryParseSubmarineReturn(string line, out string submarineName)
    {
        submarineName = string.Empty;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        string text = StripWhitespace(line);

        int start = text.IndexOf(SubmarinePrefix, StringComparison.Ordinal);
        if (start < 0)
            return false;

        int nameStart = start + SubmarinePrefix.Length;
        int nameEnd = text.IndexOf(SubmarineReturnMarker, nameStart, StringComparison.Ordinal);
        if (nameEnd <= nameStart)
            return false;

        submarineName = text[nameStart..nameEnd].Trim();
        return submarineName.Length > 0;
    }

    /// <summary>去掉时间戳与「角色名は」，只留物名起的部分。找不到「は」时返回 false。</summary>
    private static bool TryDropActorPrefix(string text, out string rest)
    {
        int start = 0;

        if (text.StartsWith('['))
        {
            int close = text.IndexOf(']', StringComparison.Ordinal);
            if (close > 0)
                start = close + 1;
        }

        int limit = Math.Min(text.Length, start + ActorNameLimit);
        if (limit <= start)
        {
            rest = string.Empty;
            return false;
        }

        int actorEnd = text.IndexOf('は', start, limit - start);
        if (actorEnd < 0)
        {
            rest = string.Empty;
            return false;
        }

        rest = text[(actorEnd + 1)..];
        return true;
    }

    private static string StripWhitespace(string text)
    {
        Span<char> buffer = text.Length <= 512 ? stackalloc char[text.Length] : new char[text.Length];
        int length = 0;

        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c))
                buffer[length++] = c;
        }

        return new string(buffer[..length]);
    }
}
