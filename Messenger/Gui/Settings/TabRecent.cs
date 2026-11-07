// TildeTools
using Dalamud.Interface.Utility.Raii;
// TildeTools ends
namespace Messenger.Gui.Settings;
public class TabRecent
{
    public static void Draw()
    {
        if(!C.UseAutoSave)
        {
            ImGuiEx.Text($"Function disabled in settings.");
            if(ImGui.Button("Enable")) C.UseAutoSave = true;
        }
        else
        {
            // TildeTools
            using(var table = ImRaii.Table("##autosave", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.NoSavedSettings | ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit)) if(table)
            // TildeTools ends
            {
                ImGui.TableSetupColumn("Recipient");
                ImGui.TableSetupColumn("When");
                ImGui.TableSetupColumn("Message", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Size");
                ImGui.TableSetupColumn("##control");
                ImGui.TableHeadersRow();

                // TildeTools
                using var id = new ImRaii.IdDisposable();
                // TildeTools ends
                var i = 0;
                foreach(var x in C.AutoSavedMessages)
                {
                    // TildeTools
                    id.Push($"Msg{i++}");
                    // TildeTools ends
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGuiEx.TextV(x.Target.GetChannelName());
                    ImGuiEx.Tooltip(x.Target.GetChannelName(true));

                    ImGui.TableNextColumn();
                    var diff = (DateTimeOffset.Now.ToUnixTimeMilliseconds() - x.Time) / 1000; //seconds
                    if(diff < 60)
                    {
                        ImGuiEx.Text($"<1m");
                    }
                    else if(diff < 60 * 60)
                    {
                        ImGuiEx.Text($"{diff / 60}m");
                    }
                    else if(diff < 60 * 60 * 24)
                    {
                        ImGuiEx.Text($"{diff / 60 / 60}h");
                    }
                    else
                    {
                        ImGuiEx.Text($"{diff / 60 / 60 / 24}d");
                    }
                    ImGuiEx.Tooltip($"{DateTimeOffset.FromUnixTimeMilliseconds(x.Time)}");

                    ImGui.TableNextColumn();
                    ImGuiEx.TextV(x.Message.ImGuiTrim());
#pragma warning disable
                    if(ImGuiEx.HoveredAndClicked("Click to copy message:\n\n" + x.Message)) Copy(x.Message);
#pragma warning restore

                    ImGui.TableNextColumn();
                    ImGuiEx.TextV($"{x.Message.Length}");
                    ImGui.TableNextColumn();
                    if(ImGuiEx.IconButton(FontAwesomeIcon.Trash))
                    {
                        new TickScheduler(() => C.AutoSavedMessages.Remove(x));
                    }
                    // TildeTools
                    id.Pop();
                    // TildeTools ends
                }

                // TildeTools: EndTable removed, the using ends it
            }
        }
    }
}
