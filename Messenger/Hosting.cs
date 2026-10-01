// TildeTools: written for this fork, not part of upstream Messenger.

using System.IO;
using ECommons.Configuration;

namespace Messenger;

public static class Hosting
{
    private const string FolderName = "Messenger";

    public static bool IsHosted { get; private set; }

    public static void HostInOwnFolder()
    {
        // Only once per load!
        // ECommons' Dispose leaves EzConfig.Config set and nulls SingletonServiceManager.Types.
        if(IsHosted)
            throw new InvalidOperationException("Messenger was already hosted this load.");

        IsHosted = true;

        EzConfig.PluginConfigDirectoryOverride = FolderName;
    }

    // Null until XIM first tick.
    // See XIM's constructor
    public static Window Settings => P?.GuiSettings;

    // Made once, since GetLogStorageFolder asks for it per conversation.
    public static DirectoryInfo DataDirectory => IsHosted
        ? field ??= Directory.CreateDirectory(Path.Combine(Svc.PluginInterface.ConfigDirectory.Parent.FullName, FolderName))
        : Svc.PluginInterface.ConfigDirectory;
}
