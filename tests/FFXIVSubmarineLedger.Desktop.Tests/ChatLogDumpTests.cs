using FFXIVSubmarineLedger.Desktop.Services;
using FFXIVSubmarineLedger.Desktop.ViewModels;
using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>
/// 调试记录的行为：读取器每次调用都返回整段累计历史，
/// 所以「只写新增的行」这条规则要是错了，文件会被同一批历史反复灌满。
/// </summary>
public sealed class ChatLogDumpTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(),
        "chatdump-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void RepeatedCumulativeReadsWriteOnlyTheNewLines()
    {
        var dump = new ChatLogDump(_folder) { Enabled = true };

        dump.Write(["first", "second"]);
        dump.Write(["first", "second", "third"]);   // 整段历史，多了一条
        dump.Write(["first", "second", "third"]);   // 没变化

        Assert.Equal(3, dump.TotalLines);

        string[] recorded = File.ReadAllLines(dump.CurrentPath!)
            .Where(line => line.StartsWith('['))
            .ToArray();

        Assert.Equal(3, recorded.Length);
        Assert.Contains("third", recorded[2]);
    }

    [Fact]
    public void TheSameLineTwiceIsRecordedTwice()
    {
        var dump = new ChatLogDump(_folder) { Enabled = true };

        dump.Write(["same line"]);
        dump.Write(["same line", "same line"]);

        Assert.Equal(2, dump.TotalLines);
    }

    [Fact]
    public void TogglingTheSettingsSwitchStartsRecording()
    {
        SettingsPageViewModel page = SettingsForm.Build(out ChatLogDump dump);

        Assert.False(dump.Enabled);

        page.DumpChatLog = true;

        Assert.True(dump.Enabled);
    }

    [Fact]
    public void TurningTheSwitchOffLeavesNoFileBehind()
    {
        var dump = new ChatLogDump(_folder);

        Assert.Equal(0, dump.Write(["anything"]));
        Assert.Null(dump.CurrentPath);
        Assert.False(Directory.Exists(_folder));
    }
}
