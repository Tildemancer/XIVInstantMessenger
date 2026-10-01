// TildeTools: written for this fork, not part of upstream Messenger.

namespace Messenger.Gui;

public static class WordLookup
{
    private static (string Line, int Index) Clicked = ("", -1);
    private static int ClickFrame = -1;

    private static string Word;

    // Call right after s[start..end] is drawn, while it's current.
    public static void Watch(string s, int start, int end)
    {
        if(!ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            return;

        // Cleared per right-click in the conversation, or clicking an emoji would show the last word.
        // Not by clicks in the open menu, which stays open.
        if(ClickFrame != ImGui.GetFrameCount() && ImGui.IsWindowHovered(ImGuiHoveredFlags.AllowWhenBlockedByPopup))
            (Clicked, ClickFrame, Word) = (("", -1), ImGui.GetFrameCount(), null);

        if(ImGui.IsItemHovered())
            Clicked = (s[start..end], PseudoMultilineInput.IndexAt(s.AsSpan(start, end - start), ImGui.GetIO().MousePos.X - ImGui.GetItemRectMin().X));
    }

    public static void DrawDefine()
    {
        if(!S.SpellCheck.IsAvailable)
            return;

        Word ??= S.SpellCheck.WordAt(Clicked.Line, Clicked.Index) is var (start, length) ? Clicked.Line.Substring(start, length) : "";
        if(Word.Length == 0)
            return;

        if(ImGui.Selectable($"Define \"{Word}\""))
            S.SpellCheck.Define(Word);

        ImGui.Separator();
    }
}
