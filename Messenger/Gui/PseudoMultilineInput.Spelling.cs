// TildeTools: written for this fork, not part of upstream Messenger.

using Dalamud.Interface.Utility.Raii;
using Dalamud.Memory;
using Messenger.Services;

namespace Messenger.Gui;

// Positioned by measuring the text, which works because the wrap newlines really are in the buffer
public unsafe partial class PseudoMultilineInput
{
    private static readonly Vector4 SpellColour = new(1f, 0.25f, 0.25f, 1f);

    private const float SpellDrop = 1.5f;
    private const float SpellThickness = 1.5f;
    private const string SpellPopup = "XIMSpellCheck";

    private string PendingWord = "";

    // Only this tells two identical typos apart
    private int PendingAt = -1;

    private bool PendingMisspelled;

    private IReadOnlyList<string> SynonymsShown;

    private (string Word, int At, string Replacement)? Used;

    private Action<string> UseFor(string word, int at) => replacement => Used = (word, at, replacement);

    private static bool? ReportedScrollLookup;

    private (nint Parent, uint Id, string Name) ScrollChild;

    // Must be called right after the input, while its rectangle is current
    private void DrawSpelling()
    {
        if(Used is var (word, at, replacement))
        {
            ReplaceWord(word, at, replacement);
            Used = null;
        }

        var misspellings = S.SpellCheck.IsAvailable && Text.Length > 0 ? S.SpellCheck.Check(Text) : null;

        if(misspellings?.Count > 0)
            Underline(misspellings);

        if(misspellings != null && ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            // Latched on the click, clearing it later empties the open menu
            var index = IndexUnderPointer();
            var (from, to) = Selected();
            var misspelling = misspellings.FirstOrDefault(m => index >= m.Start && index < m.Start + m.Length);

            // A highlighted stretch goes whole, a name or a phrase
            SynonymsShown = null;
            (PendingWord, PendingAt, PendingMisspelled) = index >= from && index < to
                ? (Text[from..to], from, misspellings.Any(m => m.Start == from && m.Length == to - from))
                : misspelling.Length > 0
                ? (misspelling.Word(Text), misspelling.Start, true)
                : S.SpellCheck.WordAt(Text, index) is var (start, length) ? (Text.Substring(start, length), start, false) : ("", -1, false);

            if(PendingWord.Length > 0 && !IsSelectingEmoji)
                ImGui.OpenPopup(SpellPopup);
        }

        DrawSpellingPopup();
    }

    // In chars, as Text is, where the box counts bytes
    // Trimmed to its letters, like a word
    private unsafe (int From, int To) Selected()
    {
        var state = ImGuiP.GetInputTextState(ImGuiP.GetItemID());
        if(state.IsNull || !ImGui.IsItemActive() || state.Stb.SelectStart == state.Stb.SelectEnd)
            return (-1, -1);

        var bytes = Encoding.UTF8.GetBytes(Text);
        var (low, high) = (Math.Min(state.Stb.SelectStart, state.Stb.SelectEnd), Math.Max(state.Stb.SelectStart, state.Stb.SelectEnd));
        var (from, to) = (Encoding.UTF8.GetCharCount(bytes, 0, Math.Min(low, bytes.Length)), Encoding.UTF8.GetCharCount(bytes, 0, Math.Min(high, bytes.Length)));

        while(from < to && !char.IsLetterOrDigit(Text[from]))
            from++;

        while(to > from && !char.IsLetterOrDigit(Text[to - 1]))
            to--;

        return from < to ? (from, to) : (-1, -1);
    }

    private Vector2? TextOrigin()
    {
        var origin = ImGui.GetItemRectMin() + ImGui.GetStyle().FramePadding;

        if(!IsMultiline)
        {
            // Not from the caret, which pins the scroll only at the line's end
            // Unfocused, the box draws from the start while the state keeps its old scroll
            var state = ImGuiP.GetInputTextState(ImGuiP.GetItemID());
            return origin - new Vector2(state.IsNull || !ImGui.IsItemActive() ? 0f : state.ScrollX, 0f);
        }

        var scroll = MultilineScrollY();
        if(float.IsNaN(scroll) && Text.AsSpan().Count('\n') >= MaxLines)
            return null;

        return origin - new Vector2(0f, float.IsNaN(scroll) ? 0f : scroll);
    }

    private int IndexUnderPointer()
    {
        if(TextOrigin() is not { } origin)
            return -1;

        var mouse = ImGui.GetIO().MousePos;
        var (lineStart, lineEnd) = (0, Text.Length);

        if(IsMultiline)
        {
            var line = (int)MathF.Floor((mouse.Y - origin.Y) / ImGui.GetTextLineHeight());
            if(line < 0)
                return -1;

            for(; line > 0; line--)
            {
                var newline = Text.IndexOf('\n', lineStart);
                if(newline < 0)
                    return -1;

                lineStart = newline + 1;
            }

            var end = Text.IndexOf('\n', lineStart);
            lineEnd = end < 0 ? Text.Length : end;
        }

        var at = IndexAt(Text.AsSpan(lineStart, lineEnd - lineStart), mouse.X - origin.X);
        return at < 0 ? -1 : lineStart + at;
    }

    internal static int IndexAt(ReadOnlySpan<char> line, float x)
    {
        if(x < 0)
            return -1;

        var (font, size) = (ImGui.GetFont(), ImGui.GetFontSize());
        var (low, high) = (0, line.Length);

        while(low < high)
        {
            var mid = (low + high + 1) / 2;

            if(ImGui.CalcTextSizeA(font, size, float.MaxValue, 0f, line[..mid], out _).X <= x)
                low = mid;
            else
                high = mid - 1;
        }

        return low;
    }

    private void Underline(IReadOnlyList<Misspelling> misspellings)
    {
        if(TextOrigin() is not { } origin)
            return;

        var (min, max) = (ImGui.GetItemRectMin(), ImGui.GetItemRectMax());
        var (lineHeight, thick) = (ImGui.GetTextLineHeight(), SpellThickness * ImGuiHelpers.GlobalScale);
        var (drawList, colour) = (ImGui.GetWindowDrawList(), ImGui.GetColorU32(SpellColour));
        var measured = MeasuredFor(misspellings);

        for(int lineStart = 0, lineIndex = 0; lineStart <= Text.Length; lineIndex++)
        {
            var newline = Text.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? Text.Length : newline;
            var top = origin.Y + lineIndex * lineHeight;
            var y = top + lineHeight + SpellDrop * ImGuiHelpers.GlobalScale;

            if(!IsMultiline)
                y = Math.Min(y, max.Y - thick);

            if(top + lineHeight >= min.Y && y >= min.Y && y <= max.Y)
                for(var i = 0; i < misspellings.Count; i++)
                {
                    var (start, length) = misspellings[i];
                    var (left, width) = measured[i];
                    var (from, to) = (Math.Max(origin.X + left, min.X), Math.Min(origin.X + left + width, max.X));

                    if(start >= lineStart && start + length <= lineEnd && !float.IsNaN(left) && to > from)
                        drawList.AddLine(new Vector2(from, y), new Vector2(to, y), colour, thick);
                }

            lineStart = lineEnd + 1;
        }
    }

    private (IReadOnlyList<Misspelling> For, ImFontPtr Face, float Size, List<(float Left, float Width)> At) Measured = ([], default, 0f, []);

    // Mirrors Chat 2's SpellUnderline.MeasuredFor
    // Left is from the mark's own line start, so the multiline box can use it too
    private List<(float Left, float Width)> MeasuredFor(IReadOnlyList<Misspelling> misspellings)
    {
        var (font, fontSize) = (ImGui.GetFont(), ImGui.GetFontSize());
        if(ReferenceEquals(Measured.For, misspellings) && Measured.Face == font && Measured.Size == fontSize)
            return Measured.At;

        float Width(ReadOnlySpan<char> span) => ImGui.CalcTextSizeA(font, fontSize, float.MaxValue, 0f, span, out _).X;

        List<(float Left, float Width)> at = new(misspellings.Count);
        var x = 0f;
        var measuredTo = 0;

        foreach(var misspelling in misspellings)
        {
            var start = misspelling.Start;
            if(start < 0 || start + misspelling.Length > Text.Length)
            {
                at.Add((float.NaN, 0f));
                continue;
            }

            // Out of order, so the prefix is measured again from the start
            if(start < measuredTo)
                (x, measuredTo) = (0f, 0);

            var lineStart = start > 0 ? Text.LastIndexOf('\n', start - 1) + 1 : 0;
            if(lineStart > measuredTo)
                (x, measuredTo) = (0f, lineStart);

            x += Width(Text.AsSpan(measuredTo, start - measuredTo));
            var width = Width(Text.AsSpan(start, misspelling.Length));
            at.Add((x, width));

            x += width;
            measuredTo = start + misspelling.Length;
        }

        Measured = (misspellings, font, fontSize, at);
        return at;
    }

    // NaN when unknown. The scroll lives on the input's child window, only reachable by name
    private float MultilineScrollY()
    {
        var (parent, id) = (ImGuiP.GetCurrentWindow(), ImGui.GetID($"##{Label}"));

        // On the input's id too: vertical tabs change it under the same window
        if(ScrollChild.Parent != (nint)parent.Handle || ScrollChild.Id != id)
            ScrollChild = ((nint)parent.Handle, id, $"{MemoryHelper.ReadStringNullTerminated((nint)parent.Name)}/##{Label}_{id:X8}");

        var child = ImGuiP.FindWindowByName(ScrollChild.Name);
        var found = !child.IsNull;

        if(ReportedScrollLookup != found)
        {
            ReportedScrollLookup = found;
            PluginLog.Information($"Spelling marks {(found ? "found" : "could not find")} the input's scroll position.");
        }

        return found ? child.Scroll.Y : float.NaN;
    }

    private void DrawSpellingPopup()
    {
        using var popup = ImRaii.Popup(SpellPopup);
        if(!popup.Success)
            return;

        KeepOnScreen();

        if(PendingWord.Length == 0)
        {
            ImGui.CloseCurrentPopup();
            return;
        }

        ImGui.TextDisabled(PendingWord);

        if(ImGui.Selectable("Synonyms", false, ImGuiSelectableFlags.DontClosePopups))
            SynonymsShown = SynonymsShown is null ? S.SpellCheck.Synonyms(PendingWord) : null;

        if(SynonymsShown is not null)
        {
            // A synonym can share a label with a correction
            using var id = ImRaii.PushId("synonyms");
            using var indent = ImRaii.PushIndent();

            if(SynonymsShown.Count == 0)
                ImGui.TextDisabled("None found");

            foreach(var synonym in SynonymsShown)
                if(ImGui.Selectable(synonym))
                {
                    S.SpellCheck.Define(synonym, PendingWord, UseFor(PendingWord, PendingAt));
                    PendingWord = string.Empty;
                    return;
                }
        }

        if(ImGui.Selectable("Define"))
        {
            S.SpellCheck.Define(PendingWord, PendingWord, UseFor(PendingWord, PendingAt));
            PendingWord = string.Empty;
            return;
        }

        if(!PendingMisspelled)
            return;

        ImGui.Separator();

        var learn = ImGui.Selectable("Add to dictionary");
        var ignore = ImGui.Selectable("Ignore for now");

        // Asking for suggestions now starts a ~100 ms lookup that the next frame's check waits on
        if(learn || ignore)
        {
            if(learn)
                S.SpellCheck.AddToDictionary(PendingWord);
            else
                S.SpellCheck.Ignore(PendingWord);

            PendingWord = string.Empty;
            return;
        }

        var suggestions = S.SpellCheck.Suggest(PendingWord);
        if(suggestions is null || suggestions.Count > 0)
            ImGui.Separator();

        if(suggestions is null)
            ImGui.TextDisabled("Looking for corrections...");
        else
            for(var i = 0; i < suggestions.Count; i++)
                if(ImGui.Selectable(suggestions[i]))
                    ReplaceWord(PendingWord, PendingAt, suggestions[i]);
    }

    // Suggestions come in after it opens, and ImGui only fits a popup to the screen on the frame it opens
    private static void KeepOnScreen()
    {
        var screen = ImGui.GetWindowViewport();
        var at = ImGui.GetWindowPos();
        var fit = Vector2.Clamp(at, screen.WorkPos, Vector2.Max(screen.WorkPos, screen.WorkPos + screen.WorkSize - ImGui.GetWindowSize()));

        if(fit != at)
            ImGui.SetWindowPos(fit);
    }

    // The clicked occurrence, else the first whole-word match, never inside a longer word.
    // Without the position the first one always won
    private void ReplaceWord(string word, int at, string replacement)
    {
        if(at < 0 || at + word.Length > Text.Length || string.CompareOrdinal(Text, at, word, 0, word.Length) != 0)
            for(at = Text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = Text.IndexOf(word, at + 1, StringComparison.Ordinal))
                if((at == 0 || !char.IsLetterOrDigit(Text[at - 1])) && (at + word.Length >= Text.Length || !char.IsLetterOrDigit(Text[at + word.Length])))
                    break;

        if(at >= 0)
        {
            Text = Text[..at] + replacement + Text[(at + word.Length)..];

            if(IsMultiline)
                SetFocusAt = at + replacement.Length;
        }

        PendingWord = "";
        PendingAt = -1;
    }
}
