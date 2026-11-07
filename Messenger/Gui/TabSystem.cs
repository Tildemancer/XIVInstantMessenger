using Messenger.Configuration;
// TildeTools
using Dalamud.Interface.Utility.Raii;
// TildeTools ends

namespace Messenger.Gui;

internal class TabSystem : Window
{
    private float Transparency = C.TransMax;
    private bool IsTransparent = true;
    private bool IsTitleColored = false;
    internal string Name = null;
    internal IEnumerable<ChatWindow> Windows => P.WindowSystemChat.Windows.Cast<ChatWindow>().Where(x => (Name == null && !C.TabWindows.Contains(x.OwningTab)) || x.OwningTab == Name);
    internal ChatWindow SelectedWindowforVerticalTabs = null;

    // TildeTools
    // Without NoFocusOnAppearing, the tab window opening on a first message takes focus from the chat box mid-typing
    public TabSystem(string name) : base($"XIV Instant Messenger - {name ?? "Default Window"}", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoFocusOnAppearing)
    // TildeTools ends
    {
        RespectCloseHotkey = false;
        IsOpen = true;
        SizeConstraints = new()
        {
            MinimumSize = new(300, 200),
            MaximumSize = new(9999, 9999)
        };
        Name = name;
    }

    internal void SetTransparency(bool isTransparent)
    {
        Transparency = !isTransparent ? C.TransMax : C.TransMin;
    }

    public override bool DrawConditions()
    {
        return C.Tabs && Windows.Any(x => x.IsOpen && !x.HideByCombat);
    }

    public override void OnClose()
    {
        foreach(var w in Windows)
        {
            w.IsOpen = false;
        }
        IsOpen = true;
    }

    public override void PreDraw()
    {
        if(C.NoResize && !ImGui.GetIO().KeyCtrl)
        {
            Flags |= ImGuiWindowFlags.NoResize;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoResize;
        }
        if(C.NoMove && !ImGui.GetIO().KeyCtrl)
        {
            Flags |= ImGuiWindowFlags.NoMove;
        }
        else
        {
            Flags &= ~ImGuiWindowFlags.NoMove;
        }
        IsTransparent = Transparency < 1f;
        if(IsTransparent) ImGui.PushStyleVar(ImGuiStyleVar.Alpha, Transparency);
        if(!C.FontNoTabs) P.FontManager.PushFont();
        if(C.ColorTitleFlashTab && Windows.Any(x => x.IsOpen && x is ChatWindow w && w.Unread))
        {
            IsTitleColored = true;
            ImGui.PushStyleColor(ImGuiCol.TitleBg, ImGuiCol.TitleBg.GetFlashColor(C.DefaultChannelCustomization));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, ImGuiCol.TitleBgActive.GetFlashColor(C.DefaultChannelCustomization));
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, ImGuiCol.TitleBgCollapsed.GetFlashColor(C.DefaultChannelCustomization));
        }
    }

    HashSet<Sender> HoveredChannels = [];

    public override void Draw()
    {
        if(P.FontManager.FontPushed && !P.FontManager.FontReady)
        {
            ImGuiEx.Text($"Loading font, please wait...");
            return;
        }
        if(ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows))
        {
            Transparency = Math.Min(C.TransMax, Transparency + C.TransDelta);
        }
        else
        {
            if(!C.DisallowTransparencyHovered && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows | ImGuiHoveredFlags.AllowWhenBlockedByPopup))
            {
                Transparency = Math.Min(C.TransMax, Transparency + C.TransDelta);
            }
            else
            {
                Transparency = Math.Max(C.TransMin, Transparency - C.TransDelta);
            }
        }
        if(!C.VerticalTabs)
        {
            // TildeTools
            using(var tabBar = ImRaii.TabBar("##MessengerTabs", ImGuiTabBarFlags.FittingPolicyScroll | ImGuiTabBarFlags.Reorderable)) if(tabBar)
            // TildeTools ends
            {
                // TildeTools
                using var tabColor = new ImRaii.ColorDisposable();
                // TildeTools ends
                foreach(var w in Windows.ToArray())
                {
                    var isOpen = w.IsOpen;
                    var flags = ImGuiTabItemFlags.None;
                    if(w.MessageHistory.ShouldSetFocus())
                    {
                        flags = ImGuiTabItemFlags.SetSelected;
                    }
                    var titleColored = false;
                    if(w.Unread)
                    {
                        titleColored = true;
                        // TildeTools
                        tabColor.Push(ImGuiCol.TabActive, ImGuiCol.TabActive.GetFlashColor(w.Cust));
                        tabColor.Push(ImGuiCol.Tab, ImGuiCol.Tab.GetFlashColor(w.Cust));
                        tabColor.Push(ImGuiCol.TabHovered, ImGuiCol.TabHovered.GetFlashColor(w.Cust));
                        // TildeTools ends
                    }
                    // TildeTools
                    using(var tab = isOpen ? ImRaii.TabItem(w.MessageHistory.HistoryPlayer.GetChannelName(!C.TabsNoWorld) + $"###{w.WindowName}", ref isOpen, flags) : default) if(tab)
                    // TildeTools ends
                    {
                        Associate(w);
                        if(titleColored)
                        {
                            // TildeTools
                            tabColor.Pop(3);
                            // TildeTools ends
                        }
                        DrawInnerWindow(w);
                        // TildeTools: EndTabItem removed, the using ends it
                    }
                    else
                    {
                        Associate(w);
                        if(ImGui.IsItemClicked())
                        {
                            w.MessageHistory.SetFocusAtNextFrame();
                        }
                        if(titleColored)
                        {
                            // TildeTools
                            tabColor.Pop(3);
                            // TildeTools ends
                        }
                    }
                    w.IsOpen = isOpen;
                }
                // TildeTools: EndTabBar removed, the using ends it
            }
        }
        else
        {
            ApplyVerticalTabAutoSelection();
            // TildeTools
            using(var layout = ImRaii.Table("##MessengerVerticalTabsLayout", 2, ImGuiTableFlags.Resizable)) if(layout)
            // TildeTools ends
            {
                float initialTabListColWidth = ImGui.GetContentRegionAvail().X * 0.2f;
                float initialTabContentColWidth = ImGui.GetContentRegionAvail().X * 0.8f;
                ImGui.TableSetupColumn("TabList", ImGuiTableColumnFlags.None, initialTabListColWidth);
                ImGui.TableSetupColumn("TabContent", ImGuiTableColumnFlags.None, initialTabContentColWidth);
                ImGui.TableNextColumn();
                // TildeTools
                using(var list = ImRaii.Table("##MessengerVerticalTabList", 1, ImGuiTableFlags.ScrollY)) if(list)
                // TildeTools ends
                {
                    ImGui.TableSetupColumn("User");
                    // TildeTools
                    using var id = new ImRaii.IdDisposable();
                    using var style = new ImRaii.StyleDisposable();
                    using var color = new ImRaii.ColorDisposable();
                    // TildeTools ends
                    foreach(var w in Windows.ToArray())
                    {
                        // TildeTools
                        id.Push(w.WindowName);
                        // TildeTools ends
                        if(w.IsOpen)
                        {
                            if(w.MessageHistory.ShouldSetFocus())
                            {
                                SelectedWindowforVerticalTabs = w;
                            }
                            ImGui.TableNextRow();
                            if(SelectedWindowforVerticalTabs == w)
                            {
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetStyle().Colors[(int)ImGuiCol.TabActive].ToUint());
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.GetStyle().Colors[(int)ImGuiCol.TabActive].ToUint());
                            }
                            else if(w.Unread)
                            {
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGuiCol.TableRowBg.GetFlashColor(w.Cust).ToUint());
                                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGuiCol.TableRowBg.GetFlashColor(w.Cust).ToUint());
                            }
                            ImGui.TableNextColumn();


                            var size = ImGuiHelpers.GetButtonSize(FontAwesomeIcon.Times.ToIconString());
                            var storeCurPos = ImGui.GetCursorPos();
                            ImGui.SetCursorPosX(ImGui.GetContentRegionAvail().X - size.X + ImGui.GetStyle().CellPadding.X);
                            var transparent = !HoveredChannels.Contains(w.MessageHistory.HistoryPlayer);
                            var hovered = false;
                            var buttonCurPos = ImGui.GetCursorPos();
                            // TildeTools
                            style.Push(ImGuiStyleVar.Alpha, 0f);
                            // TildeTools ends
                            if(ImGuiEx.SmallIconButton(FontAwesomeIcon.Times))
                            {
                                w.IsOpen = false;
                                if(SelectedWindowforVerticalTabs == w)
                                {
                                    SelectedWindowforVerticalTabs = null;
                                }
                            }
                            // TildeTools
                            style.Pop();
                            // TildeTools ends
                            if(ImGui.IsItemHovered()) hovered = true;
                            ImGui.SameLine(0, 0);
                            ImGui.SetCursorPos(storeCurPos);

                            var channelName = w.MessageHistory.HistoryPlayer.GetChannelName(!C.TabsNoWorld);
                            if(ImGui.Selectable(channelName))
                            {
                                SelectedWindowforVerticalTabs = w;
                            }
                            Associate(w);
                            ImGuiEx.Tooltip(channelName);
                            if(ImGui.IsItemHovered()) hovered = true;
                            if(!transparent)
                            {
                                ImGui.SameLine(0, 0);
                                var curPos = ImGui.GetCursorPos();
                                ImGui.SetCursorPos(buttonCurPos);
                                // TildeTools
                                color.Push(ImGuiCol.Text, ImGuiColors.DalamudRed);
                                // TildeTools ends
                                var col = ImGui.GetStyle().Colors[(int)ImGuiCol.WindowBg];
                                // TildeTools
                                color.Push(ImGuiCol.Button, col);
                                color.Push(ImGuiCol.ButtonActive, col);
                                color.Push(ImGuiCol.ButtonHovered, col);
                                // TildeTools ends
                                ImGuiEx.SmallIconButton(FontAwesomeIcon.Times);
                                // TildeTools
                                color.Pop(4);
                                // TildeTools ends
                                ImGui.SetCursorPos(curPos);
                            }
                            if(hovered)
                            {
                                HoveredChannels.Add(w.MessageHistory.HistoryPlayer);
                            }
                            else
                            {
                                HoveredChannels.Remove(w.MessageHistory.HistoryPlayer);
                            }
                        }
                        // TildeTools
                        id.Pop();
                        // TildeTools ends
                    }
                    // TildeTools: EndTable removed, the using ends it
                }
                ApplyVerticalTabAutoSelection();
                ImGui.TableNextColumn();
                if(SelectedWindowforVerticalTabs != null)
                {
                    DrawInnerWindow(SelectedWindowforVerticalTabs);
                }
                // TildeTools: EndTable removed, the using ends it
            }
        }
    }

    public readonly List<TitleBarButton> EmptyTitleBarList = [];

    public override void Update()
    {
        TitleBarButtons = EmptyTitleBarList;
    }

    public override void PostDraw()
    {
        if(!C.FontNoTabs) P.FontManager.PopFont();
        if(IsTransparent) ImGui.PopStyleVar();
        if(IsTitleColored)
        {
            ImGui.PopStyleColor(3);
            IsTitleColored = false;
        }
    }

    private void Associate(ChatWindow w)
    {
        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.OpenPopup($"Associate{w.MessageHistory.HistoryPlayer}");
        }
        // TildeTools
        using(var popup = ImRaii.Popup($"Associate{w.MessageHistory.HistoryPlayer}")) if(popup)
        // TildeTools ends
        {
            // TildeTools
            using var color = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
            // TildeTools ends
            if (ImGui.Selectable("Default window"))
            {
                C.TabWindowAssociations.Remove(w.MessageHistory.HistoryPlayer.ToString());
            }
            // TildeTools
            color.Pop();
            // TildeTools ends
            foreach (var x in C.TabWindows)
            {
                if (ImGui.Selectable(x))
                {
                    C.TabWindowAssociations[w.MessageHistory.HistoryPlayer.ToString()] = x;
                }
            }
            // TildeTools: EndPopup removed, the using ends it
        }
    }

    private void DrawInnerWindow(ChatWindow w)
    {
        w.BringToFront = false;
        w.SetPosition = false;
        w.UpdateLastFrame();
        if(C.FontNoTabs) P.FontManager.PushFont();
        w.Draw();
        TitleBarButtons = w.TitleBarButtons;
        if(C.FontNoTabs) P.FontManager.PopFont();
    }

    private void ApplyVerticalTabAutoSelection()
    {
        if (SelectedWindowforVerticalTabs != null && !SelectedWindowforVerticalTabs.IsOpen)
        {
            SelectedWindowforVerticalTabs = null;
        }
        // if we have no selected tab. Select the first open tab.
        if (SelectedWindowforVerticalTabs == null)
        {
            foreach (var w in Windows.ToArray())
            {
                if (w.IsOpen)
                {
                    SelectedWindowforVerticalTabs = w;
                    break;
                }
            }
        }
    }
}
