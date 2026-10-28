using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FFXIVSubmarineLedger.Platform.Windows;

/// <summary>
/// 单实例。双击两次就开两个进程各自读同一个库，轻则白等，重则互相锁住——
/// 表现就是「进程在跑，界面不出来」。第二个实例改为把已有窗口叫到前台再退出。
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\FFXIVSubmarineLedger.SingleInstance";

    private Mutex? _mutex;

    /// <summary>拿到独占权返回 true；已有实例在跑返回 false。</summary>
    public bool TryAcquire()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);

        if (created)
            return true;

        _mutex.Dispose();
        _mutex = null;
        return false;
    }

    /// <summary>把已有实例的窗口叫到前台。</summary>
    public static void ActivateExistingWindow(string title)
    {
        nint handle = FindWindowW(null, title);
        if (handle == nint.Zero)
            return;

        ShowWindow(handle, SwRestore);
        SetForegroundWindow(handle);
    }

    /// <summary>调试用：当前有几个进程叫这个名字。</summary>
    public static int CountProcesses(string processName) =>
        Process.GetProcessesByName(processName).Length;

    public void Dispose()
    {
        if (_mutex is null)
            return;

        _mutex.ReleaseMutex();
        _mutex.Dispose();
        _mutex = null;
    }

    private const int SwRestore = 9;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowW(string? className, string windowName);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint handle, int command);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint handle);
}
