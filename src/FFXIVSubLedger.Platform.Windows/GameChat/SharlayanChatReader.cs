using System.Diagnostics;
using System.Globalization;
using Sharlayan;
using Sharlayan.Core;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;

namespace FFXIVSubLedger.Platform.Windows.GameChat;

/// <summary>
/// 直接读游戏客户端里的聊天框内容，跳过截图与 OCR。
///
/// 走 Sharlayan（MIT）的进程外内存读取，不注入。它把聊天框缓冲区里的行按时间顺序取出来，
/// 拿到的是游戏自己渲染的文本，所以不会出现 OCR 那种认错字的问题。
///
/// **必须管理员权限**：FFXIV 的反篡改机制会拦住非提升进程的 PROCESS_VM_READ，
/// 失败时错误信息里会写清楚，由界面原样展示。
/// </summary>
public sealed class SharlayanChatReader : IDisposable
{
    private const string ProcessName = "ffxiv_dx11";

    /// <summary>首次连上时读指针就绪需要一点时间，多试几轮。</summary>
    private const int ReadAttempts = 8;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly object _gate = new();
    private MemoryHandler? _handler;

    public bool IsAttached
    {
        get
        {
            lock (_gate)
                return _handler is not null;
        }
    }

    /// <summary>
    /// 连到正在运行的游戏进程。连不上时返回 false，并给一句用户能看懂的原因。
    /// </summary>
    public bool TryAttach(out string message)
    {
        lock (_gate)
        {
            if (_handler is not null)
            {
                message = "已连接。";
                return true;
            }

            Process? game = Process.GetProcessesByName(ProcessName).FirstOrDefault();
            if (game is null)
            {
                message = "没找到正在运行的 FFXIV 客户端（窗口模式或全屏都行，但游戏要在运行）。";
                return false;
            }

            try
            {
                SharlayanConfiguration configuration = new()
                {
                    ProcessModel = new ProcessModel { Process = game },

                    // 首次读取要把聊天框里已经存在的行一起带出来，否则用户收完艇再点按钮会读到空。
                    ChatLogFirstRunReadsBacklog = true,
                };

                _handler = SharlayanMemoryManager.Instance.AddHandler(configuration);

                // 这里不要再预先 GetChatLog()：配置里开了「首次读回缓冲」，先读一次会把
                // 聊天框里已有的行白白消耗掉。首轮结果直接交给第一次 ReadNewLines。
                message = "已连接游戏聊天框。";
                return true;
            }
            catch (Exception exception)
            {
                _handler = null;
                message = $"连不上游戏聊天框：{exception.Message}。读取聊天内容需要以管理员身份运行本程序。";
                return false;
            }
        }
    }

    /// <summary>
    /// 读出上次调用之后新出现的聊天行；首次调用会连缓冲里的历史一起给出。
    ///
    /// 刚连上时读指针可能还没就绪，一次取不到不代表没有内容，所以这里等几轮
    /// （实测首次连上要等约半秒才拿到那 300 多行历史）。
    /// </summary>
    public IReadOnlyList<string> ReadNewLines()
    {
        for (int attempt = 0; attempt < ReadAttempts; attempt++)
        {
            IReadOnlyList<string> lines = ReadOnce();
            if (lines.Count > 0)
                return lines;

            if (attempt < ReadAttempts - 1)
                Thread.Sleep(RetryDelay);
        }

        return [];
    }

    private IReadOnlyList<string> ReadOnce()
    {
        lock (_gate)
        {
            if (_handler is null)
                return [];

            ChatLogResult result = _handler.Reader.GetChatLog();
            var lines = new List<string>();

            foreach (ChatLogItem item in result.ChatLogItems)
            {
                // Line 是「(玩家名 )消息」的形态，与游戏日志里的写法一致，时间戳单独给，
                // 拼成和截图里一样的前缀，下游解析器就不用区分来源。
                string text = item.TimeStamp == default
                    ? item.Line
                    : $"[{item.TimeStamp.ToString("HH:mm", CultureInfo.InvariantCulture)}] {item.Line}";

                if (!string.IsNullOrWhiteSpace(text))
                    lines.Add(text.Trim());
            }

            return lines;
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _handler = null;
        }
    }
}
