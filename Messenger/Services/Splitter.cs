// TildeTools: written for this fork, not part of upstream Messenger.

using Dalamud.Plugin.Ipc;

namespace Messenger.Services;

// Numbers are the IPC contract
public enum SplitTake
{
    // Carry on as without a splitter
    NotTaken = 0,

    // Send nothing, the box can be cleared
    Queued = 1,

    // Send nothing, keep the text
    Refused = 2,
}

public sealed class Splitter : IDisposable
{
    private const int RequiredApiVersion = 2;

    private readonly ICallGateSubscriber<int> ApiVersionGate = Svc.PluginInterface.GetIpcSubscriber<int>("TildeTools.Split.ApiVersion");
    private readonly ICallGateSubscriber<string, int, int> SendLineStatusGate = Svc.PluginInterface.GetIpcSubscriber<string, int, int>("TildeTools.Split.SendLineStatus");
    private readonly ICallGateSubscriber<object?> AvailableGate = Svc.PluginInterface.GetIpcSubscriber<object?>("TildeTools.Split.Available");

    private bool Present;

    public Splitter()
    {
        AvailableGate.Subscribe(Refresh);
        Refresh();
    }

    private void Refresh() => Present = SpellCheck.Try(() => ApiVersionGate.InvokeFunc() >= RequiredApiVersion, false);

    // 500 = a chat line's cap in bytes, prefix included
    public SplitTake TrySend(string line) =>
        Present ? SpellCheck.Try(() => (SplitTake)SendLineStatusGate.InvokeFunc(line, 500), SplitTake.NotTaken) : SplitTake.NotTaken;

    public void Dispose() => AvailableGate.Unsubscribe(Refresh);
}
