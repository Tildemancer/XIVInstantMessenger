using ECommons.Configuration;
using ECommons.Funding;
using ECommons.Reflection;

namespace Messenger.Gui.Settings;

internal class GuiSettings : Window
{
    internal readonly TabHistory TabHistory = new();
    internal readonly TabSettings TabSettings = new();
    internal readonly TabStyle TabStyle = new();
    internal readonly TabFonts TabFonts = new();
    internal readonly TabIndividual TabIndividual = new();
    internal readonly TabDebug TabDebug = new();

    public GuiSettings() : base($"{P.Name} v{Svc.PluginInterface.Manifest.AssemblyVersion}")
    {
        SizeConstraints = new()
        {
            MaximumSize = new(99999, 99999),
            MinimumSize = new(500, 300)
        };
    }

    public override void Draw()
    {
        // TildeTools
        // NightmareXIV's Patreon banner moved to TT's Credits tab, same old same old...
        ImGuiEx.EzTabBar("MessengerBar", null,
        // TildeTools ends
            ("History", TabHistory.Draw, null, true),
            ("Engagements", TabEngagement.Draw, null, true),
            ("Settings", TabSettings.Draw, null, true),
            ("Style", TabStyle.Draw, null, true),
            ("Fonts", TabFonts.Draw, null, true),
            ("Recent", TabRecent.Draw, null, true),
            ("Generic channels", TabIndividual.Draw, null, true),
            // TildeTools
            // A null name makes EzTabBar skip the tab.
            // Hosted, EzIPC's prefix is TT's, so no translator would answer.
            (Hosting.IsHosted ? null : "Translation", TabTranslation.Draw, null, true),
            // TildeTools ends
            ("Log", InternalLog.PrintImgui, ImGuiColors.DalamudGrey3, false),
            ("Debug", TabDebug.Draw, ImGuiColors.DalamudGrey3, true)
            );
    }

    public override void OnOpen()
    {
        base.OnOpen();
    }

    public override void OnClose()
    {
        base.OnClose();
        EzConfig.Save();
    }
}
