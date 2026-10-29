using Xunit;

namespace FFXIVSubmarineLedger.Domain.Tests;

/// <summary>
/// 会切换显示语言的测试必须串行跑：<c>Loc</c> 是进程级单例，
/// 两个类并行改语言会互相踩，表现是断言随机失败（实际踩到过）。
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LocalizationCollection
{
    public const string Name = "显示语言";
}
