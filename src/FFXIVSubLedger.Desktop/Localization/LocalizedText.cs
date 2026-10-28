using System.ComponentModel;
using FFXIVSubLedger.Application.Localization;

namespace FFXIVSubLedger.Desktop.Localization;

/// <summary>
/// 一条界面文案的绑定源：持有一个键，<see cref="Value" /> 按当前语言取值。
///
/// 为什么不直接绑到 <see cref="Loc" /> 的索引器：Avalonia 12 的绑定引擎不会因为
/// <c>PropertyChanged("Item[]")</c>（甚至属性名为空的全量通知）重新求值索引器路径，
/// 结果是换语言后界面保持原样——用户报过「切到日语还有很多中文」。用普通属性通知才可靠。
/// </summary>
public sealed class LocalizedText : INotifyPropertyChanged
{
    internal static readonly List<WeakReference<LocalizedText>> Live = [];

    static LocalizedText() => Loc.Instance.LanguageChanged += (_, _) => RelocalizeAll();

    private readonly string _key;

    public LocalizedText(string key)
    {
        _key = key;

        lock (Live)
            Live.Add(new WeakReference<LocalizedText>(this));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value => Loc.Get(_key);

    internal void Relocalize() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));

    /// <summary>换语言后通知所有还活着的绑定。视图销毁后弱引用自然失效，不会留着。</summary>
    private static void RelocalizeAll()
    {
        lock (Live)
        {
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                if (Live[i].TryGetTarget(out LocalizedText? text))
                    text.Relocalize();
                else
                    Live.RemoveAt(i);
            }
        }
    }
}
