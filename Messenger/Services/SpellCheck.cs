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
    private const int RequiredApiVersion = 6;

    private readonly ICallGateSubscriber<int> ApiVersionGate = Svc.PluginInterface.GetIpcSubscriber<int>("TildeTools.Spell.ApiVersion");
    private readonly ICallGateSubscriber<string, List<int>> CheckGate = Svc.PluginInterface.GetIpcSubscriber<string, List<int>>("TildeTools.Spell.Check");
    private readonly ICallGateSubscriber<string, int, List<int>> WordAtGate = Svc.PluginInterface.GetIpcSubscriber<string, int, List<int>>("TildeTools.Spell.WordAt");
    private readonly ICallGateSubscriber<string, bool> DefineGate = Svc.PluginInterface.GetIpcSubscriber<string, bool>("TildeTools.Spell.Define");
    private readonly ICallGateSubscriber<object?> AvailableGate = Svc.PluginInterface.GetIpcSubscriber<object?>("TildeTools.Spell.Available");
    private readonly ICallGateSubscriber<string, string, bool, Action<string>, bool> DrawMenuGate = Svc.PluginInterface.GetIpcSubscriber<string, string, bool, Action<string>, bool>("TildeTools.Spell.DrawMenu");

    private readonly Dictionary<string, List<Misspelling>> Checked = [];
    private const int MostChecked = 16;

    public SpellCheck()
    {
        AvailableGate.Subscribe(Refresh);
        Refresh();
    }

    public bool IsAvailable { get; private set; }

    // Runs on TildeTools.Spell.Available, which also fires after a word is added or ignored. See SpellIpc.Announce
    private void Refresh()
    {
        IsAvailable = Try(() => ApiVersionGate.InvokeFunc() >= RequiredApiVersion, false);
        Checked.Clear();
    }

    public IReadOnlyList<Misspelling> Check(string text)
    {
        // Cached per text so that two windows' boxes don't evict each other every frame.
        // Same text, same list back, which MeasuredFor keys on.
        if(Checked.TryGetValue(text, out var found))
            return found;

        if(Checked.Count >= MostChecked)
            Checked.Clear();

        var result = Checked[text] = [];

        // Not Try, because its lambda would capture text and allocate on every call. Same deal as everywhere else.
        try
        {
            // Newlines sent as spaces, same length, so the offsets still fit the caller's text.
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

    public (int Start, int Length)? WordAt(string text, int index) =>
        Try<(int, int)?>(() => WordAtGate.InvokeFunc(text, index) is [var start, var length] ? (start, length) : null, null);

    // True once TT is done with the word, or gone.
    // Not Try, because its lambda allocates and the menu draws every frame. Deja vu...
    public bool DrawMenu(string id, string word, bool misspelled, Action<string> use)
    {
        try
        {
            return DrawMenuGate.InvokeFunc(id, word, misspelled, use);
        }
        catch
        {
            return true;
        }
    }

    public void Define(string word) => Try(() => DefineGate.InvokeFunc(word), false);

    // Gates throw while TT unloads or their module is off.
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
