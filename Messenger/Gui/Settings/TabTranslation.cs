using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// TildeTools
using Dalamud.Interface.Utility.Raii;
// TildeTools ends

namespace Messenger.Gui.Settings;
public static unsafe class TabTranslation
{
    public static void Draw()
    {
        ImGuiEx.TextWrapped("If you'd like your messages to be automatically translated, you can select a translation provider here. ");
        ImGuiEx.SetNextItemFullWidth();
        // TildeTools
        using(var combo = ImRaii.Combo("##tr", C.TranslationProvider ?? "- Translation Disabled -")) if(combo)
        // TildeTools ends
        {
            if(ImGui.Selectable("- Translation Disabled -", C.TranslationProvider == null))
            {
                C.TranslationProvider = null;
            }
            HashSet<string> translators = [];
            try
            {
                S.IPCProvider.OnAvailableTranslatorsRequest(translators);
            }
            catch(Exception e)
            {
                e.Log();
            }
            foreach(var x in translators)
            {
                if(ImGui.Selectable(x, C.TranslationProvider == x))
                {
                    C.TranslationProvider = x;
                }
            }
            // TildeTools: EndCombo removed, the using ends it
        }

        if(C.TranslationProvider != null)
        {
            ImGui.Checkbox("Auto-translate all incoming messages", ref C.TranslateAuto);
            ImGui.Checkbox("Automatically translate messages loaded from history", ref C.TranslateHistory);
            try
            {
                S.IPCProvider.OnTranslatorSettingsDraw(C.TranslationProvider);
            }
            catch(Exception e)
            {
                e.Log();
            }
        }
    }
}