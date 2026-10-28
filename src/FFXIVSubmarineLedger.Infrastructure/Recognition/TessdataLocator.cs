namespace FFXIVSubmarineLedger.Infrastructure.Recognition;

/// <summary>
/// 找 OCR 模型目录。模型随程序分发，不依赖系统装的 OCR 组件或语言包。
/// </summary>
public static class TessdataLocator
{
    public const string ModelFileName = "jpn.traineddata";

    /// <summary>默认位置：程序目录下的 <c>Data/tessdata</c>。</summary>
    public static string DefaultDirectory => Path.Combine(AppContext.BaseDirectory, "Data", "tessdata");

    /// <summary>
    /// 返回可用的模型目录。目录或模型文件缺失时直接报错，并把期望路径写进消息——
    /// 这条错误用户看得到，得说清缺什么、在哪找。
    /// </summary>
    public static string Resolve(string? directory = null)
    {
        string resolved = string.IsNullOrWhiteSpace(directory) ? DefaultDirectory : directory;

        if (!Directory.Exists(resolved))
            throw new DirectoryNotFoundException($"找不到 OCR 模型目录：{resolved}");

        string model = Path.Combine(resolved, ModelFileName);
        if (!File.Exists(model))
            throw new FileNotFoundException($"OCR 模型目录里缺少 {ModelFileName}：{resolved}", model);

        return resolved;
    }
}
