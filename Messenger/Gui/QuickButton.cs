using FFXIVClientStructs.FFXIV.Component.GUI;
using Messenger.Configuration;
// TildeTools
using Dalamud.Interface.Utility.Raii;
// TildeTools ends

namespace Messenger.Gui;

internal unsafe class QuickButton : Window
{
    internal QuickButton() : base("MessengerQuickButton",
        ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.AlwaysUseWindowPadding
        , true)
    {
        IsOpen = true;
        RespectCloseHotkey = false;
    }

    public override bool DrawConditions()
    {
        AtkUnitBase* addon = null;
        var ret = C.QuickOpenButton
            && (C.AddonName == string.Empty || (TryGetAddonByName(C.AddonName, out addon)
            && addon->IsVisible));
        if(ret)
        {
            Position = new Vector2(C.QuickOpenPositionX2, C.QuickOpenPositionY2);
            if(addon != null)
            {
                Position += new Vector2(addon->X, addon->Y);
            }
        }
        return ret;
    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowMinSize, Vector2.Zero);
    }

    public override void Draw()
    {
        if(C.QuickOpenButtonOnTop)
        {
            CImGui.igBringWindowToDisplayFront(CImGui.igGetCurrentWindow());
        }
        // TildeTools
        using var color = new ImRaii.ColorDisposable();
        color.Push(ImGuiCol.Button, Vector4.Zero);
        color.Push(ImGuiCol.ButtonActive, Vector4.Zero);
        color.Push(ImGuiCol.ButtonHovered, Vector4.Zero);
        // TildeTools ends
        var col = false;
        if(P.Hidden)
        {
            // TildeTools
            color.Push(ImGuiCol.Text, ImGuiCol.Text.GetFlashColor(C.DefaultChannelCustomization));
            // TildeTools ends
            col = true;
        }
        if(ImGuiEx.IconButton(FontAwesomeIcon.MailBulk))
        {
            ImGui.OpenPopup("Select target");
        }
        if(col)
        {
            // TildeTools
            color.Pop();
            // TildeTools ends
        }
        // TildeTools
        color.Pop(3);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(7, 7));
        using(var popup = ImRaii.Popup("Select target")) if(popup)
        // TildeTools ends
        {
            // TildeTools
            // Its own color object, so a throw pops these before the popup ends
            using var textColor = new ImRaii.ColorDisposable();
            // TildeTools ends
            if(S.MessageProcessor.Chats.Count == 0)
            {
                ImGuiEx.Text("There is nothing here yet...");
            }
            else
            {
                ImGuiEx.WithTextColor(ImGuiColors.DalamudGrey, delegate
                {
                    if(ImGui.Selectable("Close all chat windows"))
                    {
                        Svc.Commands.ProcessCommand("/xim close");
                    }
                });
                var tsize = ImGui.CalcTextSize("");
                Sender? toRem = null;
                foreach(var x in S.MessageProcessor.Chats)
                {
                    var cur = ImGui.GetCursorPos();
                    if(ImGui.Selectable($"{x.Key.GetChannelName()} ({x.Value.Messages.Count})", false, ImGuiSelectableFlags.None, new Vector2(200f.Scale(), tsize.Y)))
                    {
                        P.Hidden = false;
                        x.Value.ChatWindow.IsOpen = true;
                        x.Value.SetFocusAtNextFrame();
                        if(Svc.Condition[ConditionFlag.InCombat])
                        {
                            x.Value.ChatWindow.KeepInCombat = true;
                            Notify.Info("This chat will not be hidden in combat");
                        }
                    }
                    ImGui.SameLine(0, 0);
                    // TildeTools
                    textColor.Push(ImGuiCol.Text, ImGuiColors.DalamudRed);
                    // TildeTools ends
                    if(ImGui.Selectable($"   ##{x.Key.GetChannelName()}", false, ImGuiSelectableFlags.DontClosePopups))
                    {
                        toRem = x.Key;
                    }
                    // TildeTools
                    textColor.Pop();
                    // TildeTools ends
                }
                if(toRem != null)
                {
                    Utils.Unload(toRem.Value);
                    toRem = null;
                }
            }
            // TildeTools: EndPopup removed, the using ends it
        }
        // TildeTools: PopStyleVar removed, the using pops it
    }

    public override void PostDraw()
    {
        ImGui.PopStyleVar(2);
    }
}
