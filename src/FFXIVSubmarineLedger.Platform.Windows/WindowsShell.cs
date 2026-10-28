using System.Diagnostics;

namespace FFXIVSubmarineLedger.Platform.Windows;

/// <summary>需要交给系统处理的动作（打开目录等），集中在这里。</summary>
public static class WindowsShell
{
    /// <summary>用资源管理器打开目录；目录不存在就先建出来。</summary>
    public static void OpenFolder(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    /// <summary>用默认浏览器打开网址（发布页之类）。</summary>
    public static void OpenUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
