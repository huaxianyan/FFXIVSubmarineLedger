namespace FFXIVSubLedger.Application.Errors;

/// <summary>用户输入或状态不满足业务规则时抛出。UI 直接展示 Message。</summary>
public sealed class ValidationException(string message) : Exception(message);
