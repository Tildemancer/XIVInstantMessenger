// TildeTools: written for this fork, not part of upstream Messenger.

using Dalamud.Plugin.Ipc;

namespace Messenger.Services;

// The numbers are the IPC contract, so they must match TT's SplitTake.
public enum SplitTake
{
    // XIM sends it itself.
    NotTaken = 0,

    // XIMs ends nothing, and the box can be cleared.
    Queued = 1,

    // XIM sends nothing and keeps the text.
    Refused = 2,
}

public sealed class Splitter : IDisposable
{
    private const int RequiredApiVersion = 2;

    private readonly ICallGateSubscriber<int> ApiVersionGate = Svc.PluginInterface.GetIpcSubscriber<int>("TildeTools.Split.ApiVersion");
    private readonly ICallGateSubscriber<string, int, int> SendLineStatusGate = Svc.PluginInterface.GetIpcSubscriber<string, int, int>("TildeTools.Split.SendLineStatus");
    private readonly ICallGateSubscriber<object?> AvailableGate = Svc.PluginInterface.GetIpcSubscriber<object?>("TildeTools.Split.Available");
    private readonly ICallGateSubscriber<string, int, List<string>> SplitLineGate = Svc.PluginInterface.GetIpcSubscriber<string, int, List<string>>("TildeTools.Split.SplitLine");

    private bool Present;

    public int Generation { get; private set; }

    public Splitter()
    {
        AvailableGate.Subscribe(Refresh);
        Refresh();
    }

    private void Refresh() => (Present, Generation) = (SpellCheck.Try(() => ApiVersionGate.InvokeFunc() >= RequiredApiVersion, false), Generation + 1);

    // 500 = a chat line's cap in bytes, prefix included
    public SplitTake TrySend(string line) =>
        Present ? SpellCheck.Try(() => (SplitTake)SendLineStatusGate.InvokeFunc(line, 500), SplitTake.NotTaken) : SplitTake.NotTaken;

    public List<string> Parts(string line) => Present ? SpellCheck.Try(() => SplitLineGate.InvokeFunc(line, 500), []) : [];

    public void Dispose() => AvailableGate.Unsubscribe(Refresh);
}
