using Avalonia.Headless.XUnit;
using FFXIVSubmarineLedger.Desktop;
using FFXIVSubmarineLedger.Domain.Settings;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 全新安装能起来：用户 2026-11-28 反馈，装完第一次启动就弹「no such table: settings」。
/// 组合根在装配时就要读设置定界面语言，而建表排在它后面，空库上必然查不到表。
/// </summary>
[Collection(LocalizationCollection.Name)]
public sealed class FreshInstallTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "subledger-fresh-" + Guid.NewGuid().ToString("N"));

    [AvaloniaFact]
    public async Task Composition_StartsOnAnEmptyDataDirectory()
    {
        ApplicationComposition composition = ApplicationComposition.Create(_directory);

        // 能读到设置就说明 settings 表已经建出来了；读不到会抛 SqliteException。
        AppSettings settings = await composition.Settings.GetAsync();

        Assert.NotNull(settings);
        Assert.True(File.Exists(composition.Database.DatabasePath), "空目录里应该建出库文件");
        Assert.True(Directory.Exists(_directory));
    }

    [AvaloniaFact]
    public async Task Composition_CanBeCreatedTwiceOnTheSameDirectory()
    {
        // 第二次启动走的是「表已存在」那条路，建表语句必须是幂等的。
        ApplicationComposition.Create(_directory);
        ApplicationComposition second = ApplicationComposition.Create(_directory);

        Assert.NotNull(await second.Settings.GetAsync());
    }

    public void Dispose()
    {
        // 连接池会拿住库文件，不先清掉就删不动目录。
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
