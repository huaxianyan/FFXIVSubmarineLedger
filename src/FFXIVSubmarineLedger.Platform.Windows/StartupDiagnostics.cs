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
        string line = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");

        // 两个实例同时写是常态（第二个实例也要留痕），所以共享写、失败重试几次，
        // 别因为文件被占着就把这一行丢掉——丢掉的往往正是出事那一次。
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                string path = LogPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                using var stream = new FileStream(
                    path,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);

                using var writer = new StreamWriter(stream);
                writer.Write(line);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50);
            }
        }
    }

    public static void RecordException(string stage, Exception exception) =>
        Record($"{stage} 失败：{exception}");

    /// <summary>弹一个系统消息框。只能用 Win32：这时候 Avalonia 可能还没起来。</summary>
    public static void ShowError(string title, string message)
    {
        int result = MessageBoxW(nint.Zero, message, title, MbIconError | MbSetForeground);
        Record($"消息框返回 {result}");
    }

    private const uint MbIconError = 0x00000010;
    private const uint MbSetForeground = 0x00010000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(nint owner, string text, string caption, uint type);
}
