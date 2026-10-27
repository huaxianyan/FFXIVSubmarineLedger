namespace FFXIVSubLedger.Platform.Windows;

/// <summary>Windows 下的数据目录与文件路径。Domain/Application 不依赖这些。</summary>
public static class WindowsPaths
{
    public static string DefaultDataDirectory()
    {
        string root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "FFXIVSubLedger");
    }

    public static string DatabasePath(string dataDirectory)
        => Path.Combine(dataDirectory, "subledger.db");
}
