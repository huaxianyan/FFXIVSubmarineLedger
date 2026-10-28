using System.Globalization;
using System.Reflection;
using System.Text.Json;
using FFXIVSubLedger.Domain.Settings;

namespace FFXIVSubLedger.Application.Localization;

/// <summary>一种可用语言。<paramref name="Name" /> 取自该语言文件自己的 <c>language.name</c>。</summary>
public sealed record LanguageInfo(string Tag, string Name);

/// <summary>
/// 界面文案。按语言存一份 JSON，随程序分发（嵌入式资源 <c>language-*.json</c>），用键取文字。
///
/// **加一门语言不用改代码**：放一个 <c>language-&lt;任意名&gt;.json</c>，里面自带
/// <c>language.tag</c>（如 <c>en</c>）与 <c>language.name</c>（如 <c>English</c>），
/// 键与现有语言对齐即可（有测试比对键集合）。
///
/// 只影响显示：日志解析用哪种语言由解析器决定，跟这里无关。
/// </summary>
public sealed class Loc : System.ComponentModel.INotifyPropertyChanged
{
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = LoadTables();

    private string _language = ItemDisplayLanguage.Fallback;

    private Loc()
    {
    }

    public static Loc Instance { get; } = new();

    /// <summary>所有随程序分发的语言，按文件名排序。</summary>
    public static IReadOnlyList<LanguageInfo> Available { get; } =
    [
        .. Tables.Select(x => new LanguageInfo(x.Key, x.Value.GetValueOrDefault("language.name", x.Key))),
    ];

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? LanguageChanged;

    /// <summary>所有语言的键集合，供测试比对是否对齐。</summary>
    public static IReadOnlyDictionary<string, IReadOnlyCollection<string>> KeySets =>
        Tables.ToDictionary(x => x.Key, x => (IReadOnlyCollection<string>)[.. x.Value.Keys]);

    /// <summary>当前语言标签。</summary>
    public string Language => _language;

    /// <summary>按当前语言取文案。缺键时依次退到英语、默认语言，最后返回键本身。</summary>
    public string this[string key]
    {
        get
        {
            foreach (string language in FallbackChain())
            {
                if (Tables.TryGetValue(language, out Dictionary<string, string>? table)
                    && table.TryGetValue(key, out string? value))
                {
                    return value;
                }
            }

            return key;
        }
    }

    /// <summary>换语言。传入未随程序分发的标签时保持原样，不把界面切到空文案上。</summary>
    public bool SetLanguage(string language)
    {
        if (!Tables.ContainsKey(language) || _language == language)
            return Tables.ContainsKey(language);

        _language = language;
        ItemDisplayLanguage.Current = language;
        // 只通知语言本身变了：Avalonia 12 的绑定引擎不会因为 "Item[]" 重新求值索引器路径，
        // 所以 XAML 文案不绑索引器，而是每处一个 LocalizedText（普通属性通知）。
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Language)));
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public static string Get(string key) => Instance[key];

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Instance[key], args);

    /// <summary>当前语言 → 英语 → 默认语言。任一语言文件缺键时用它兜底。</summary>
    private IEnumerable<string> FallbackChain()
    {
        yield return _language;

        if (_language != "en")
            yield return "en";

        if (_language != ItemDisplayLanguage.Fallback)
            yield return ItemDisplayLanguage.Fallback;

        foreach (string other in Tables.Keys)
        {
            if (other != _language)
                yield return other;
        }
    }

    private static Dictionary<string, Dictionary<string, string>> LoadTables()
    {
        var tables = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        Assembly assembly = typeof(Loc).Assembly;

        string[] resources =
        [
            .. assembly.GetManifestResourceNames()
                .Where(x => x.Contains("language-", StringComparison.Ordinal)
                            && x.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal),
        ];

        if (resources.Length == 0)
            throw new InvalidOperationException("没有找到任何语言资源，检查 csproj 的 EmbeddedResource。");

        foreach (string resource in resources)
        {
            Dictionary<string, string> table = ReadTable(assembly, resource)
                ?? throw new InvalidOperationException($"语言资源 {resource} 不是合法的键值表。");

            if (!table.TryGetValue("language.tag", out string? tag) || string.IsNullOrWhiteSpace(tag))
                throw new InvalidOperationException($"语言资源 {resource} 缺少 language.tag，无法确定它是哪种语言。");

            if (!tables.TryAdd(tag, table))
                throw new InvalidOperationException($"语言标签 {tag} 重复了（{resource}）。");
        }

        return tables;
    }

    private static Dictionary<string, string>? ReadTable(Assembly assembly, string resource)
    {
        using Stream stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"读不到语言资源 {resource}。");

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
    }
}
