using Xunit;

namespace FFXIVSubmarineLedger.Desktop.Tests;

/// <summary>会切换显示语言的测试必须串行跑，<c>Loc</c> 是进程级单例。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationCollection
{
    public const string Name = "显示语言";
}
