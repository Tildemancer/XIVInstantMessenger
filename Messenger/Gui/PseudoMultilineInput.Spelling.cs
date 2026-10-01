// TildeTools: written for this fork, not part of upstream Messenger.

using Dalamud.Interface.Utility.Raii;
using Dalamud.Memory;
using Messenger.Services;

namespace Messenger.Gui;

// Placed by measuring the text, since this box wraps with real newlines in its buffer.
public unsafe partial class PseudoMultilineInput
{
    private static readonly Vector4 SpellColor = new(1f, 0.25f, 0.25f, 1f);

    private const float SpellDrop = 1.5f;
    private const float SpellThickness = 1.5f;
    private const string SpellPopup = "XIMSpellCheck";

    private string PendingWord = "";

    private bool PendingMisspelled;

    // Set when the menu opens so Use isn't allocated every frame.
    // TT keeps synonyms and corrections per MenuId.
    private string MenuId = "";
    private int MenusOpened;
    private Action<string> Use = _ => { };

    // Held for the next frame's input, the menu and Define window call Use elsewhere.
    private (string Word, int At, string Replacement)? Used;

    // Kept as a delegate, passing the method group allocates a new one every frame.
    private ImGui.ImGuiInputTextCallbackDelegate Singleline;

    private static bool? ReportedScrollLookup;

    private (nint Parent, uint Id, string Name) ScrollChild;

    // Must be called right after the input, while its rectangle is still current!
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
            // Taken on the click, clearing it later would empty the open menu.
            var index = IndexUnderPointer();
            var (from, to) = Selected();
            var misspelling = misspellings.FirstOrDefault(m => index >= m.Start && index < m.Start + m.Length);

            // A selection goes whole, tjat way a name or phrase is looked up as one.
            // pickedAt tells identical typos apart.
            (PendingWord, var pickedAt, PendingMisspelled) = index >= from && index < to
                ? (Text[from..to], from, misspellings.Any(m => m.Start == from && m.Length == to - from))
                : misspelling.Length > 0
                ? (misspelling.Word(Text), misspelling.Start, true)
                : S.SpellCheck.WordAt(Text, index) is var (start, length) ? (Text.Substring(start, length), start, false) : ("", -1, false);

            var picked = PendingWord;
            (MenuId, Use) = ($"Messenger {++MenusOpened}", replacement => Used = (picked, pickedAt, replacement));

            if(PendingWord.Length > 0 && !IsSelectingEmoji)
                ImGui.OpenPopup(SpellPopup);
        }

        DrawSpellingPopup();
    }

    // The state's selection is in characters like Text, the callback's positions are bytes.
    // Trimmed to its letters like a word, edge spaces won't count.
    private (int From, int To) Selected()
    {
        var state = ImGuiP.GetInputTextState(ImGuiP.GetItemID());
        if(state.IsNull || !ImGui.IsItemActive() || state.Stb.SelectStart == state.Stb.SelectEnd)
            return (-1, -1);

        var (low, high) = (Math.Min(state.Stb.SelectStart, state.Stb.SelectEnd), Math.Max(state.Stb.SelectStart, state.Stb.SelectEnd));
        var (from, to) = (Math.Min(low, Text.Length), Math.Min(high, Text.Length));

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
            // Scroll comes from the input state, since the caret only pins it at the end of the line.
            // Unfocused, the box draws from the start while the state keeps its old scroll.
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
        var (drawList, color) = (ImGui.GetWindowDrawList(), ImGui.GetColorU32(SpellColor));
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
                        drawList.AddLine(new Vector2(from, y), new Vector2(to, y), color, thick);
                }

            lineStart = lineEnd + 1;
        }
    }

    private (IReadOnlyList<Misspelling> For, ImFontPtr Face, float Size, List<(float Left, float Width)> At) Measured = ([], default, 0f, []);

    // Mirrors C2's SpellUnderline.MeasuredFor.
    // Left is from the mark's line start, so the multiline box can use it too.
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

            // Out of order, so measuring starts over from the start of the text.
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

    // NaN when unknown.
    // The scroll lives on the input's child window, LF name.
    private float MultilineScrollY()
    {
        var (parent, id) = (ImGuiP.GetCurrentWindow(), ImGui.GetID($"##{Label}"));

        // Keyed on the input id too, vertical tabs change it under the same window.
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

    // TT draws the entries into it. See SpellMenu
    private void DrawSpellingPopup()
    {
        using var popup = ImRaii.Popup(SpellPopup);
        if(!popup.Success)
            return;

        if(PendingWord.Length == 0 || S.SpellCheck.DrawMenu(MenuId, PendingWord, PendingMisspelled, Use))
        {
            PendingWord = "";
            ImGui.CloseCurrentPopup();
        }
    }

    // XIM's box had no callback, so this one only puts the caret back after a correction.
    private int SinglelineCallback(ref ImGuiInputTextCallbackData data)
    {
        if(SetFocusAt > -1 && SetFocusAt <= data.BufTextLen)
            data.CursorPos = data.SelectionStart = data.SelectionEnd = SetFocusAt;

        SetFocusAt = -1;
        return 0;
    }

    private void ReplaceWord(string word, int at, string replacement)
    {
        if(at < 0 || at + word.Length > Text.Length || string.CompareOrdinal(Text, at, word, 0, word.Length) != 0)
            for(at = Text.IndexOf(word, StringComparison.Ordinal); at >= 0; at = Text.IndexOf(word, at + 1, StringComparison.Ordinal))
                if((at == 0 || !char.IsLetterOrDigit(Text[at - 1])) && (at + word.Length >= Text.Length || !char.IsLetterOrDigit(Text[at + word.Length])))
                    break;

        if(at >= 0)
        {
            Text = Text[..at] + replacement + Text[(at + word.Length)..];

            // The caret goes back past the space after the word.
            // A space is added at the end, since we don't consider the word 'finished' till we see one.
            var end = at + replacement.Length;
            if(end == Text.Length)
                Text += " ";

            // A wrap is a space Callback turned into '\n', so skip that too.
            SetFocusAt = Encoding.UTF8.GetByteCount(Text.AsSpan(0, Text[end] is ' ' or '\n' ? end + 1 : end));
        }

        PendingWord = "";
    }
}
