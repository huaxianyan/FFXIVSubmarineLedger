using System.Runtime.InteropServices;

namespace FFXIVSubmarineLedger.Platform.Windows;

/// <summary>
/// 启动期的自检与记录。双击启动看不到界面时，用户唯一的线索是「进程在跑」，
/// 所以启动路径上的每一步都要留下痕迹，出错也要能弹出来，而不是静默退出。
/// </summary>
public static class StartupDiagnostics
{
    /// <summary>启动日志放在数据目录下，和数据库挨着，方便一起打包回传。</summary>
    public static string LogPath => Path.Combine(WindowsPaths.DefaultDataDirectory(), "logs", "startup.log");

    public static void Record(string message)
    {
        try
        {
            string path = LogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string line = string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");

            File.AppendAllText(path, line);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 记日志本身失败不该再抛：这条路已经是在处理故障了。
        }
    }

    public static void RecordException(string stage, Exception exception) =>
        Record($"{stage} 失败：{exception}");

    /// <summary>弹一个系统消息框。只能用 Win32：这时候 Avalonia 可能还没起来。</summary>
    public static void ShowError(string title, string message) =>
        MessageBoxW(nint.Zero, message, title, MbIconError | MbSetForeground);

    private const uint MbIconError = 0x00000010;
    private const uint MbSetForeground = 0x00010000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint owner, string text, string caption, uint type);
}
