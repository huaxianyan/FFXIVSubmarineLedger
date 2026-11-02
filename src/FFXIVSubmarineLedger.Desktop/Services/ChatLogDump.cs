using System.Text;

namespace FFXIVSubmarineLedger.Desktop.Services;

/// <summary>
/// 调试用：把从游戏聊天框读到的原文追加到 txt。
///
/// 目的是让其它语言的客户端用户把样本发回来，照着补对应语言的解析规则。
/// 读取器每次调用都返回整段累计历史，所以这里按「同一行出现的次数」去重：
/// 只写这一轮比之前多出来的那几条，不会把整个缓冲反复写进文件。
/// </summary>
public sealed class ChatLogDump(string dataDirectory)
{
    private readonly object _gate = new();

    /// <summary>每条原文已经写过几次。缓冲是环形表，旧行会被挤掉，这里只增不减。</summary>
    private readonly Dictionary<string, int> _written = new(StringComparer.Ordinal);

    private string? _path;
    private int _total;

    /// <summary>由设置页控制。关掉时 <see cref="Write"/> 直接返回，不碰磁盘。</summary>
    public bool Enabled { get; set; }

    public string Folder { get; } = Path.Combine(dataDirectory, "chatlog");

    public string? CurrentPath
    {
        get
        {
            lock (_gate)
            {
                return _path;
            }
        }
    }

    public int TotalLines
    {
        get
        {
            lock (_gate)
            {
                return _total;
            }
        }
    }

    /// <summary>返回这次真正写进文件的行数。</summary>
    public int Write(IReadOnlyList<string> lines)
    {
        if (!Enabled || lines.Count == 0)
        {
            return 0;
        }

        lock (_gate)
        {
            var fresh = new List<string>();
            var counted = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                counted.TryGetValue(line, out int seen);
                counted[line] = seen + 1;

                _written.TryGetValue(line, out int already);
                if (seen + 1 > already)
                {
                    fresh.Add(line);
                }
            }

            foreach ((string line, int count) in counted)
            {
                _written.TryGetValue(line, out int already);
                if (count > already)
                {
                    _written[line] = count;
                }
            }

            if (fresh.Count == 0)
            {
                return 0;
            }

            try
            {
                _path ??= CreateFile();
                string stamp = DateTime.Now.ToString("HH:mm:ss");
                File.AppendAllLines(
                    _path,
                    fresh.Select(line => $"[{stamp}] {line}"),
                    new UTF8Encoding(false));
                _total += fresh.Count;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // 盘满或目录没权限时不打断收艇，关掉开关让用户能在设置页看到状态。
                Enabled = false;
                return 0;
            }

            return fresh.Count;
        }
    }

    private string CreateFile()
    {
        Directory.CreateDirectory(Folder);

        string path = Path.Combine(Folder, $"chatlog-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        File.WriteAllLines(
            path,
            [
                "# FFXIV Submarine Ledger — 聊天记录样本",
                $"# 开始记录：{DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                "# 下面是程序从游戏聊天框读到的原文，用来适配其它语言的客户端。",
                "# 每行的格式是 [记录时刻] 原文；记录时刻是程序读到的时刻，不是游戏内时间。",
                string.Empty,
            ],
            new UTF8Encoding(false));

        return path;
    }
}
