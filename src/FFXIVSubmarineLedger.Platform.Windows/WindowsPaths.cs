namespace FFXIVSubmarineLedger.Platform.Windows;

/// <summary>Windows 下的数据目录与文件路径。Domain/Application 不依赖这些。</summary>
public static class WindowsPaths
{
    /// <summary>本程序的数据目录名，与仓库、程序集同名。</summary>
    private const string DirectoryName = "FFXIVSubmarineLedger";

    /// <summary>改名前用的目录名。</summary>
    private const string LegacyDirectoryName = "FFXIVSubLedger";

    public static string DefaultDataDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), DirectoryName);

    /// <summary>
    /// 把旧目录的数据搬到新目录。只在「旧的有、新的没有」时搬：两个都有说明用户已经在新目录
    /// 里有了数据，这时候乱动反而危险，交给用户自己处理。
    /// </summary>
    public static void MigrateLegacyDataDirectory()
    {
        string legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LegacyDirectoryName);

        string current = DefaultDataDirectory();

        if (Directory.Exists(legacy) && !Directory.Exists(current))
            Directory.Move(legacy, current);
    }

    public static string DatabasePath(string dataDirectory)
        => Path.Combine(dataDirectory, "subledger.db");
}
