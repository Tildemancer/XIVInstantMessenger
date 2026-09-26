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
        // ECommons can't start twice per load: Dispose leaves EzConfig.Config set and nulls SingletonServiceManager.Types
        if(IsHosted)
            throw new InvalidOperationException("Messenger was already hosted this load.");

        IsHosted = true;

        EzConfig.PluginConfigDirectoryOverride = FolderName;
    }

    // Null until Messenger's first tick
    // See Messenger's constructor
    public static Window Settings => P?.GuiSettings;

    // Made once, GetLogStorageFolder asks per conversation
    public static DirectoryInfo DataDirectory => IsHosted
        ? field ??= Directory.CreateDirectory(Path.Combine(Svc.PluginInterface.ConfigDirectory.Parent.FullName, FolderName))
        : Svc.PluginInterface.ConfigDirectory;
}
