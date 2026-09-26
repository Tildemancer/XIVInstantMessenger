// TildeTools: written for this fork, not part of upstream Messenger.

using Dalamud.Plugin.Ipc;

namespace Messenger.Services;

public readonly record struct Misspelling(int Start, int Length)
{
    public string Word(string text) =>
        Start >= 0 && Length > 0 && Start + Length <= text.Length
            ? text.Substring(Start, Length)
            : string.Empty;
}

public sealed class SpellCheck : IDisposable
{
    private const int RequiredApiVersion = 1;

    private readonly ICallGateSubscriber<int> ApiVersionGate = Svc.PluginInterface.GetIpcSubscriber<int>("TildeTools.Spell.ApiVersion");
    private readonly ICallGateSubscriber<string, List<int>> CheckGate = Svc.PluginInterface.GetIpcSubscriber<string, List<int>>("TildeTools.Spell.Check");
    private readonly ICallGateSubscriber<string, List<string>?> SuggestGate = Svc.PluginInterface.GetIpcSubscriber<string, List<string>?>("TildeTools.Spell.Suggest");
    private readonly ICallGateSubscriber<string, bool> AddGate = Svc.PluginInterface.GetIpcSubscriber<string, bool>("TildeTools.Spell.AddToDictionary");
    private readonly ICallGateSubscriber<string, bool> IgnoreGate = Svc.PluginInterface.GetIpcSubscriber<string, bool>("TildeTools.Spell.Ignore");
    private readonly ICallGateSubscriber<string, int, List<int>> WordAtGate = Svc.PluginInterface.GetIpcSubscriber<string, int, List<int>>("TildeTools.Spell.WordAt");
    private readonly ICallGateSubscriber<string, List<string>> SynonymsGate = Svc.PluginInterface.GetIpcSubscriber<string, List<string>>("TildeTools.Spell.Synonyms");
    private readonly ICallGateSubscriber<string, string, Action<string>, bool> DefineGate = Svc.PluginInterface.GetIpcSubscriber<string, string, Action<string>, bool>("TildeTools.Spell.Define");
    private readonly ICallGateSubscriber<object?> AvailableGate = Svc.PluginInterface.GetIpcSubscriber<object?>("TildeTools.Spell.Available");

    private readonly Dictionary<string, List<Misspelling>> Checked = [];
    private const int MostChecked = 16;

    public SpellCheck()
    {
        AvailableGate.Subscribe(Refresh);
        Refresh();
    }

    public bool IsAvailable { get; private set; }

    // Also after a word is added or ignored, see SpellIpc.Announce
    private void Refresh()
    {
        IsAvailable = Try(() => ApiVersionGate.InvokeFunc() >= RequiredApiVersion, false);
        Checked.Clear();
    }

    // See PseudoMultilineInput.DrawSpelling
    public IReadOnlyList<Misspelling> Check(string text)
    {
        // By text, so two windows' boxes don't evict each other every frame
        // The same list back for the same text, which MeasuredFor keys on
        if(Checked.TryGetValue(text, out var found))
            return found;

        if(Checked.Count >= MostChecked)
            Checked.Clear();

        var result = Checked[text] = [];

        // Not Try: its lambda captures text, so every call allocated, cache hits too
        try
        {
            // Newlines as spaces, same length so the offsets fit the caller's text
            // Flattened: start, length, start, length
            var flat = CheckGate.InvokeFunc(text.Replace('\n', ' '));
            for(var i = 0; i + 1 < flat.Count; i += 2)
                result.Add(new Misspelling(flat[i], flat[i + 1]));
        }
        catch
        {
            IsAvailable = false;
        }

        return result;
    }

    // Null while TildeTools is still looking
    // Not Try: asked every frame the menu is open, and Try's lambda allocates
    public IReadOnlyList<string>? Suggest(string word)
    {
        try
        {
            return SuggestGate.InvokeFunc(word);
        }
        catch
        {
            return [];
        }
    }

    public void AddToDictionary(string word) => Try(() => AddGate.InvokeFunc(word), false);

    // Until restart, without learning it
    // The checker filters, so every box agrees
    public void Ignore(string word) => Try(() => IgnoreGate.InvokeFunc(word), false);

    public (int Start, int Length)? WordAt(string text, int index) =>
        Try<(int, int)?>(() => WordAtGate.InvokeFunc(text, index) is [var start, var length] ? (start, length) : null, null);

    public IReadOnlyList<string> Synonyms(string word) => Try(() => SynonymsGate.InvokeFunc(word), []);

    // Opens TildeTools' Define window
    // Its Use button calls use with original's replacement, none when use is null
    public void Define(string word, string original, Action<string> use) => Try(() => DefineGate.InvokeFunc(word, original, use), false);

    // Gates throw while TildeTools unloads or their module is off
    internal static T Try<T>(Func<T> call, T failed)
    {
        try
        {
            return call();
        }
        catch
        {
            return failed;
        }
    }

    public void Dispose() => AvailableGate.Unsubscribe(Refresh);
}
