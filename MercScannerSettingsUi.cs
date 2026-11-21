using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using UiHelpers = SquirrelUi;

namespace MercScanner;

public partial class MercScanner
{
    private bool _clearStrategyOpen;
    private bool _loadStrategyOpen;
    private bool _showAdvancedSettings;
    private static string _editingSliderId;
    private static bool _sliderEditFocus;

    public override void DrawSettings()
    {
        UiHelpers.PushSettingsStyle();
        try
        {
            _showAdvancedSettings = ToggleRow("Advanced mode", ref _showAdvancedSettings, "topAdv",
                "Unlocks the rest of this window.");
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 44f);
            ImGui.TextColored(_showAdvancedSettings
                    ? new Vector4(0.49f, 1f, 0.25f, 1f)
                    : new Vector4(1f, 0.35f, 0.35f, 1f),
                _showAdvancedSettings ? "ON" : "OFF");
            ImGui.SameLine();
            ImGui.TextColored(UiHelpers.Muted,
                "Shows colors, timers, tier ranks and support ratings.");
            ImGui.Separator();

            if (ImGui.BeginChild("##msSide", new Vector2(140, 0), ImGuiChildFlags.None, ImGuiWindowFlags.None))
            {
                SettingsTab("General", 0);
                SettingsTab("Overlay", 1);
                SettingsTab("Strategy", 2);
                SettingsTab("Flame Link", 3);
            }
            ImGui.EndChild();
            ImGui.SameLine();
            if (ImGui.BeginChild("##msMain", new Vector2(0, 0), ImGuiChildFlags.Borders, ImGuiWindowFlags.None))
            {
                var ver = GetType().Assembly.GetName().Version?.ToString(2);
                UiHelpers.HouseHeader("MercScanner", ver);
                if (_settingsTab == 0) DrawGeneralTab();
                else if (_settingsTab == 1) DrawOverlayTab();
                else if (_settingsTab == 2) DrawStrategyTab();
                else DrawFlameLinkTab();
            }
            ImGui.EndChild();
        }
        finally
        {
            UiHelpers.PopSettingsStyle();
        }
    }

    private void SettingsTab(string label, int id)
    {
        if (UiHelpers.SidebarRow(label, null, _settingsTab == id, "mstab" + id, 0))
        {
            _settingsTab = id;
            _editingSliderId = null;
        }
    }

    // Layout D: switch at x=0, every label starts at the same 44px gutter.
    private static bool ToggleRow(string label, ToggleNode node, string id, string help = null)
    {
        var start = ImGui.GetCursorScreenPos();
        float lineH = ImGui.GetTextLineHeight();
        float rowH = lineH + 8f;
        float w = ImGui.GetContentRegionAvail().X;

        ImGui.SetCursorScreenPos(start);
        var pillValue = UiHelpers.PillMini(node.Value, "ms_pill_" + id, rowH);

        ImGui.SetCursorScreenPos(start + new Vector2(44f, 0f));
        ImGui.InvisibleButton("##ms_row_" + id, new Vector2(w - 44f, rowH));
        bool rowClicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();

        ImGui.SetCursorScreenPos(start + new Vector2(44f, (rowH - lineH) * 0.5f));
        ImGui.TextColored(hovered ? new Vector4(1f, 1f, 1f, 1f) : UiHelpers.Muted, label);
        if (help != null)
        {
            ImGui.SetCursorScreenPos(start + new Vector2(44f + ImGui.CalcTextSize(label).X + 8f, (rowH - lineH) * 0.5f));
            UiHelpers.HelpMarker(help);
        }

        if (pillValue != node.Value) node.Value = pillValue;
        else if (rowClicked) node.Value = !node.Value;

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + rowH));
        return node.Value;
    }

    private static bool ToggleRow(string label, ref bool value, string id, string help = null)
    {
        var start = ImGui.GetCursorScreenPos();
        float lineH = ImGui.GetTextLineHeight();
        float rowH = lineH + 8f;
        float w = ImGui.GetContentRegionAvail().X;

        ImGui.SetCursorScreenPos(start);
        var pillValue = UiHelpers.PillMini(value, "ms_pill_" + id, rowH);

        ImGui.SetCursorScreenPos(start + new Vector2(44f, 0f));
        ImGui.InvisibleButton("##ms_row_" + id, new Vector2(w - 44f, rowH));
        bool rowClicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();

        ImGui.SetCursorScreenPos(start + new Vector2(44f, (rowH - lineH) * 0.5f));
        ImGui.TextColored(hovered ? new Vector4(1f, 1f, 1f, 1f) : UiHelpers.Muted, label);
        if (help != null)
        {
            ImGui.SetCursorScreenPos(start + new Vector2(44f + ImGui.CalcTextSize(label).X + 8f, (rowH - lineH) * 0.5f));
            UiHelpers.HelpMarker(help);
        }

        if (pillValue != value) value = pillValue;
        else if (rowClicked) value = !value;

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + rowH));
        return value;
    }

    private static void AlignedSlider(RangeNode<int> node, string label, string id, string help = null)
    {
        float value = node.Value;
        DrawAlignedSlider(label, ref value, node.Min, node.Max, id, true, help, v => node.Value = (int)Math.Round(v));
    }

    private static void AlignedSlider(RangeNode<float> node, string label, string id, string help = null)
    {
        float value = node.Value;
        DrawAlignedSlider(label, ref value, node.Min, node.Max, id, false, help, v => node.Value = v);
    }

    private static void DrawAlignedSlider(string label, ref float value, float min, float max, string id, bool isInt, string help, Action<float> commit)
    {
        var start = ImGui.GetCursorScreenPos();
        float lineH = ImGui.GetTextLineHeight();
        float h = lineH + 8f;
        float w = ImGui.GetContentRegionAvail().X;
        const float trackStart = 270f;
        const float valueGutter = 44f;
        float trackEnd = w - valueGutter;
        float trackY = start.Y + h * 0.5f;

        // Hit area is the track only; clicking the label must not jump the value to min.
        ImGui.SetCursorScreenPos(start + new Vector2(trackStart, 0f));
        ImGui.InvisibleButton("##ms_slider_" + id, new Vector2(trackEnd - trackStart, h));
        bool active = ImGui.IsItemActive();
        bool hovered = ImGui.IsItemHovered();

        var text = isInt ? ((int)Math.Round(value)).ToString() : value.ToString("0.0");
        var textSize = ImGui.CalcTextSize(text);
        var textPos = new Vector2(start.X + w - textSize.X, start.Y + (h - lineH) * 0.5f);
        bool editing = _editingSliderId == id;

        if (!editing && ImGui.IsMouseHoveringRect(textPos - new Vector2(6f, 3f), textPos + textSize + new Vector2(6f, 3f))
            && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _editingSliderId = id;
            _sliderEditFocus = true;
            editing = true;
        }

        var dl = ImGui.GetWindowDrawList();
        uint accent = ImGui.GetColorU32(new Vector4(0.486f, 0.360f, 1f, 1f));
        uint trackCol = ImGui.GetColorU32(new Vector4(0.165f, 0.180f, 0.208f, 1f));
        uint labelCol = ImGui.GetColorU32(new Vector4(0.91f, 0.92f, 0.93f, 1f));
        uint valueCol = ImGui.GetColorU32(new Vector4(0.72f, 0.68f, 1f, 1f));

        float norm = max > min ? Math.Clamp((value - min) / (max - min), 0f, 1f) : 0f;
        float lineStart = start.X + trackStart;
        float lineEnd = start.X + trackEnd;
        float dotX = lineStart + norm * (lineEnd - lineStart);

        dl.AddText(new Vector2(start.X + 44f, start.Y + (h - lineH) * 0.5f), labelCol, label);
        if (help != null)
        {
            ImGui.SetCursorScreenPos(new Vector2(start.X + 44f + ImGui.CalcTextSize(label).X + 8f, start.Y + (h - lineH) * 0.5f));
            UiHelpers.HelpMarker(help);
        }
        if (!editing) dl.AddText(textPos, valueCol, text);
        dl.AddLine(new Vector2(lineStart, trackY), new Vector2(lineEnd, trackY), trackCol, 3f);
        if (dotX > lineStart)
            dl.AddLine(new Vector2(lineStart, trackY), new Vector2(dotX, trackY), accent, 3f);
        dl.AddCircleFilled(new Vector2(dotX, trackY), hovered || active ? 6.5f : 5.5f, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 1f)));

        if (editing)
        {
            ImGui.SetCursorScreenPos(new Vector2(start.X + w - 64f, start.Y + (h - lineH) * 0.5f - 2f));
            ImGui.SetNextItemWidth(64f);
            bool justOpened = false;
            if (_sliderEditFocus)
            {
                ImGui.SetKeyboardFocusHere();
                _sliderEditFocus = false;
                justOpened = true;
            }
            bool enter = ImGui.InputFloat("##ms_edit_" + id, ref value, 0f, 0f, "%g", ImGuiInputTextFlags.EnterReturnsTrue);
            value = Math.Clamp(value, min, max);
            commit(value);
            if (enter || (!justOpened && ImGui.IsItemDeactivated()))
            {
                value = Math.Clamp(value, min, max);
                commit(value);
                _editingSliderId = null;
            }
        }
        else if (active && max > min && lineEnd > lineStart)
        {
            float mx = ImGui.GetMousePos().X;
            float nn = Math.Clamp((mx - lineStart) / (lineEnd - lineStart), 0f, 1f);
            var next = min + nn * (max - min);
            if (Math.Abs(next - value) > 0.0001f)
            {
                value = next;
                commit(value);
            }
        }
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + h));
    }

    private static void SliderRow(RangeNode<int> node, string label, string help = null) =>
        AlignedSlider(node, label, "ms_" + label, help);

    private static void SliderRow(RangeNode<float> node, string label, string help = null) =>
        AlignedSlider(node, label, "ms_" + label, help);

    private static void ThemedHotkeyButton(HotkeyNodeV2 node, string label, string id)
    {
        var pos = ImGui.GetCursorScreenPos();
        float h = ImGui.GetTextLineHeight() + 8f;

        // Keep the HUD's key-capture behaviour, but hide its default button chrome.
        ImGui.PushStyleColor(ImGuiCol.Button, 0u);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0u);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0u);
        ImGui.PushStyleColor(ImGuiCol.Text, 0u);
        ImGui.SetCursorScreenPos(pos);
        node.DrawPickerButton(new string(' ', Math.Max(8, label.Length + 4)) + "##" + id + "_picker");
        ImGui.PopStyleColor(4);

        ImGui.SetCursorScreenPos(pos);
        UiHelpers.Button(label, id, 0);
        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + h));
    }

    private static void ColorRow(string label, ColorNode node, string help = null)
    {
        var start = ImGui.GetCursorScreenPos();
        float lineH = ImGui.GetTextLineHeight();
        float rowH = lineH + 8f;
        var c = node.Value;
        var vv = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        ImGui.SetCursorScreenPos(start);
        if (ImGui.ColorEdit4("##ms_color_" + label, ref vv, ImGuiColorEditFlags.NoInputs))
            node.Value = new SharpDX.Color((byte)(vv.X * 255f), (byte)(vv.Y * 255f), (byte)(vv.Z * 255f), (byte)(vv.W * 255f));
        ImGui.SetCursorScreenPos(start + new Vector2(44f, (rowH - lineH) * 0.5f));
        ImGui.TextColored(UiHelpers.Muted, label);
        if (help != null)
        {
            ImGui.SetCursorScreenPos(start + new Vector2(44f + ImGui.CalcTextSize(label).X + 8f, (rowH - lineH) * 0.5f));
            UiHelpers.HelpMarker(help);
        }
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + rowH));
    }

    private void DrawGeneralTab()
    {
        UiHelpers.SectionHeader("Setup");
        if (UiHelpers.ActionButton("Load preset", "ms_loadRecommended", 1,
                "Replaces your skill lists, tier ranks and support ratings with the preset.")) _loadStrategyOpen = true;
        DrawLoadStrategyPopup();

        UiHelpers.SectionHeader("Plugin");
        Settings.Enable.Value = ToggleRow("Enable", Settings.Enable, "enable",
            "Turns scanning and overlays on or off.");
        Settings.IgnoreFullscreenPanels.Value = ToggleRow("Ignore fullscreen panels", Settings.IgnoreFullscreenPanels, "ignoreFull",
            "Skips mercenaries behind fullscreen windows like the atlas or stash.");
        Settings.IgnoreLargePanels.Value = ToggleRow("Ignore large panels", Settings.IgnoreLargePanels, "ignoreLarge",
            "Skips mercenaries covered by large UI windows.");

        UiHelpers.SectionHeader("Status");
        var mercCount = 0;
        if (GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Monster, out var monsters))
            foreach (var monster in monsters)
                if (IsMercenary(monster)) mercCount++;
        ImGui.BulletText($"Mercenaries in memory: {mercCount}");
        ImGui.BulletText($"Tiers: {Settings.MercenaryTiers.Count(x => x.Value > 0)} / {Settings.MercenaryTiers.Count}");
        ImGui.BulletText($"Supports: {Settings.SupportRatings.Count} global, {Settings.SupportSkillOverrides.Count} combo overrides");
        ImGui.BulletText(Settings.AutoCastFlameLink.Value ? "Flame Link auto-cast is on" : "Flame Link auto-cast is off");
    }

    private void DrawOverlayTab()
    {
        UiHelpers.SectionHeader("Unhired mercenaries");
        Settings.ShowEntityOverlays.Value = ToggleRow("Show mercenary overlays", Settings.ShowEntityOverlays, "entityOverlays",
            "Draws bars, rings and rank text on unhired mercenaries.");
        Settings.ShowHpBar.Value = ToggleRow("Show HP bar", Settings.ShowHpBar, "hpBar",
            "Draws a health bar above each mercenary.");
        if (_showAdvancedSettings) ColorRow("HP bar color", Settings.HpBarColor);
        Settings.ShowEntityFrames.Value = ToggleRow("Show stat rings", Settings.ShowEntityFrames, "statRings",
            "Draws rings that show the mercenary's main stat.");
        Settings.ShowEntityTier.Value = ToggleRow("Show tier label", Settings.ShowEntityTier, "entityTier",
            "Shows the S/A/B/C rank above each mercenary.");
        if (_showAdvancedSettings)
            Settings.UseLiveStatInference.Value = ToggleRow("Infer stats from live data", Settings.UseLiveStatInference, "liveInfer",
                "Reads stats from the game instead of relying on the name alone.");
        Settings.ShowMercLevel.Value = ToggleRow("Show level", Settings.ShowMercLevel, "showLevel",
            "Adds the mercenary level to the label.");
        SliderRow(Settings.MaxMercDistance, "Max distance (m)", "How far from you an overlay can appear.");
        Settings.ShowOffScreenIndicators.Value = ToggleRow("Off-screen indicators", Settings.ShowOffScreenIndicators, "offscreen",
            "Draws arrows to mercenaries outside the view.");
        ImGui.BeginDisabled(!Settings.ShowOffScreenIndicators.Value);
        SliderRow(Settings.MaxIndicatorDistance, "Indicator distance (m)", "How far away an off-screen arrow can appear.");
        ImGui.EndDisabled();

        UiHelpers.SectionHeader("Name labels");
        Settings.HighlightMercenary.Value = ToggleRow("Highlight mercenary names", Settings.HighlightMercenary, "highlightNames",
            "Colors name tags by their main stat.");
        if (_showAdvancedSettings)
        {
            ColorRow("Strength label color", Settings.StrColor);
            ColorRow("Dexterity label color", Settings.DexColor);
            ColorRow("Intelligence label color", Settings.IntColor);
        }

        UiHelpers.SectionHeader("Hired mercenaries");
        Settings.ShowHiredMercOverlays.Value = ToggleRow("Show hired merc overlays", Settings.ShowHiredMercOverlays, "hiredOverlays",
            "Draws overlays on your current mercenary.");
        Settings.ShowHiredMercHpBar.Value = ToggleRow("Show HP bar", Settings.ShowHiredMercHpBar, "hiredHpBar",
            "Draws a health bar above your mercenary.");
        Settings.ShowHiredMercActionPanel.Value = ToggleRow("Show action panel", Settings.ShowHiredMercActionPanel, "actionPanel",
            "Shows the mercenary's skill bar and action state.");
        Settings.ShowSkillCooldowns.Value = ToggleRow("Show skill cooldowns", Settings.ShowSkillCooldowns, "cooldowns",
            "Adds cooldown bars to the mercenary's skills.");
        Settings.ShowHiredMercBuffs.Value = ToggleRow("Show filtered buffs", Settings.ShowHiredMercBuffs, "buffs",
            "Shows buffs that pass the filter list.");

        if (_showAdvancedSettings)
        {
            UiHelpers.SectionHeader("Cooldown bars");
            ImGui.BeginDisabled(!Settings.ShowSkillCooldowns.Value);
            var style = Settings.SkillCooldownBarStyle.Value;
            var styleStart = ImGui.GetCursorScreenPos();
            float styleLineH = ImGui.GetTextLineHeight();
            ImGui.SetCursorScreenPos(styleStart + new Vector2(44f, 0f));
            ImGui.TextColored(UiHelpers.Muted, "Bar style");
            ImGui.SetCursorScreenPos(styleStart + new Vector2(44f + ImGui.CalcTextSize("Bar style").X + 8f, 0f));
            UiHelpers.HelpMarker("Where the cooldown bar sits relative to the skill name.");
            ImGui.SetCursorScreenPos(styleStart + new Vector2(270f, 0f));
            ImGui.SetNextItemWidth(180f);
            if (ImGui.BeginCombo("##ms_cdBarStyle", CooldownBarStyleLabels[Math.Clamp(style, 0, CooldownBarStyleLabels.Length - 1)]))
            {
                for (var i = 0; i < CooldownBarStyleLabels.Length; i++)
                    if (ImGui.Selectable(CooldownBarStyleLabels[i], style == i))
                        Settings.SkillCooldownBarStyle.Value = i;
                ImGui.EndCombo();
            }
            ImGui.SetCursorScreenPos(new Vector2(styleStart.X, styleStart.Y + styleLineH + 8f));
            ImGui.EndDisabled();
        }
    }

    private void DrawStrategyTab()
    {
        UiHelpers.SectionHeader("Tiers");
        Settings.ShowTierText.Value = ToggleRow("Show tier label", Settings.ShowTierText, "tierText",
            "Shows the rank on offer window rows.");
        if (_showAdvancedSettings)
        {
            ColorRow("S tier", Settings.STierColor);
            ColorRow("A tier", Settings.ATierColor);
            ColorRow("B tier", Settings.BTierColor);
            ColorRow("C tier", Settings.CTierColor);

            UiHelpers.SectionHeader("Preset data");
            var strategyStart = ImGui.GetCursorScreenPos();
            var strategy = UiHelpers.ButtonRow(
                ("Load preset", "ms_loadAuto", 1),
                ("Clear data", "ms_restore", 2));
            var strategyAfter = ImGui.GetCursorScreenPos();
            ImGui.SetCursorScreenPos(new Vector2(strategyStart.X, strategyAfter.Y));
            if (strategy == 0) _loadStrategyOpen = true;
            else if (strategy == 1) _clearStrategyOpen = true;
            DrawClearStrategyPopup();
            DrawLoadStrategyPopup();

            UiHelpers.SectionHeader("Mercenary tiers");
            DrawTierDropdowns();

            UiHelpers.SectionHeader("Tier frame");
            Settings.ShowTierFrame.Value = ToggleRow("Show background fill", Settings.ShowTierFrame, "tierFrame",
                "Fills the rank badge with a translucent color.");
            SliderRow(Settings.TierFrameFillOpacity, "Fill opacity", "How strong the badge fill is.");
            Settings.ShowTierSnake.Value = ToggleRow("Show snake effect (S tier)", Settings.ShowTierSnake, "tierSnake",
                "Animates a moving band on S tier badges.");
            SliderRow(Settings.TierSnakeSpeed, "Snake speed", "How fast the band moves.");
            SliderRow(Settings.TierSnakeIntensity, "Snake intensity", "How bright the band is.");
        }

        UiHelpers.SectionHeader("Skills and auras");
        Settings.ShowAllSkills.Value = ToggleRow("Show all skills", Settings.ShowAllSkills, "showAllSkills",
            "Shows every skill on offer. Turn off to see only matched skills and auras.");
        Settings.SeparateAuraDisplay.Value = ToggleRow("Separate aura display", Settings.SeparateAuraDisplay, "separateAura",
            "Puts auras in their own list.");
        Settings.AutoDetectAuras.Value = ToggleRow("Detect known auras", Settings.AutoDetectAuras, "autoAura",
            "Matches auras against the built-in list.");
        Settings.ShowAuraTimers.Value = ToggleRow("Show aura timers", Settings.ShowAuraTimers, "auraTimers",
            "Adds time remaining to active auras.");
        Settings.HighlightEncounterPanelBorders.Value = ToggleRow("Highlight offer window skills", Settings.HighlightEncounterPanelBorders, "encounterHighlight",
            "Colors matching skills and supports in the offer window.");

        UiHelpers.SectionHeader("Aura presets");
        AuraPresetRow("Wanted auras", ref _selectedAuraIndex, AddAuraToFilter, "wanted",
            "Adds preset auras to the wanted skills list.");
        AuraPresetRow("Bad auras", ref _badSelectedAuraIndex, AddBadAuraToFilter, "bad",
            "Adds preset auras to the bad skills list.");

        UiHelpers.SectionHeader("Wanted skills");
        DrawFilterListEditor("wanted", Settings.SkillFilter, ref _skillFilterInput, AddSkillFilterEntry, Settings.HighlightSkillColor.Value);

        UiHelpers.SectionHeader("Bad skills");
        DrawFilterListEditor("bad", Settings.BadSkillFilter, ref _badSkillFilterInput, AddBadSkillFilterEntry, Settings.BadSkillColor.Value);
        ImGui.TextColored(UiHelpers.Muted, "Matches by substring and ignores case. Bad skills win if both lists match.");

        if (!_showAdvancedSettings) return;

        UiHelpers.SectionHeader("Skill colors");
        ColorRow("Wanted / highlight", Settings.HighlightSkillColor);
        ColorRow("Bad skill", Settings.BadSkillColor);
        ColorRow("Default skill", Settings.DefaultSkillColor);
        ColorRow("Monster skill", Settings.MonsterSkillColor);
        ColorRow("Active aura", Settings.AuraActiveColor);
        ColorRow("Inactive aura", Settings.AuraInactiveColor);
        ColorRow("Overlay background", Settings.BackgroundColor);
        if (UiHelpers.ActionButton("Reset colors", "resetColors", 0,
                "Restores the default skill and aura colors.")) ResetSkillColors();

        UiHelpers.SectionHeader("Support ratings");
        ImGui.TextWrapped("Support gems get a border and a letter grade on each linked skill. Combo grades take priority over global ones.");
        ImGui.Text($"{Settings.SupportRatings.Count} rated supports, {Settings.SupportSkillOverrides.Count} combo overrides.");
    }

    private void AuraPresetRow(string title, ref int selectedIndex, Action<string> addAura, string id, string help = null)
    {
        var rowStart = ImGui.GetCursorScreenPos();
        ImGui.TextColored(new Vector4(0.58f, 0.53f, 0.92f, 1f), title);
        if (help != null)
        {
            ImGui.SetCursorScreenPos(rowStart + new Vector2(ImGui.CalcTextSize(title).X + 8f, 0f));
            UiHelpers.HelpMarker(help);
        }
        ImGui.SetNextItemWidth(170f);
        var preview = selectedIndex >= 0 && selectedIndex < KnownAuraDisplayNames.Length
            ? KnownAuraDisplayNames[selectedIndex]
            : "Select aura...";
        if (ImGui.BeginCombo("##ms_quick_" + id, preview))
        {
            for (var i = 0; i < KnownAuraDisplayNames.Length; i++)
            {
                var isSelected = selectedIndex == i;
                var auraColor = GetSkillEntryColor(KnownAuraDisplayNames[i], Settings.DefaultSkillColor.Value);
                ImGui.PushStyleColor(ImGuiCol.Text, ToImGuiColor(auraColor));
                var selected = ImGui.Selectable(KnownAuraDisplayNames[i], isSelected);
                ImGui.PopStyleColor();
                if (selected)
                {
                    selectedIndex = i;
                    addAura(KnownAuraDisplayNames[i]);
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.SameLine();
        var add = UiHelpers.ButtonRow(
            ("Add aura", "ms_addAura_" + id, 1),
            ("Add all", "ms_addAll_" + id, 0));
        if (add == 0 && selectedIndex >= 0 && selectedIndex < KnownAuraDisplayNames.Length)
            addAura(KnownAuraDisplayNames[selectedIndex]);
        else if (add == 1)
            foreach (var (_, displayName) in KnownAuraEntries) addAura(displayName);

        // ButtonRow advances X to the end of its last button; restore the row gutter.
        var after = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, after.Y));
    }

    private void DrawFilterListEditor(string id, List<string> targetList, ref string input, Action<string> addEntry, SharpDX.Color entryColor)
    {
        var rowStart = ImGui.GetCursorScreenPos();
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 100f);
        if (ImGui.InputTextWithHint("##ms_" + id + "_input", "Type a skill name...", ref input, 64,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
        {
            addEntry(input);
            input = "";
        }
        ImGui.SameLine();
        var action = UiHelpers.ButtonRow(
            ("Add", "ms_add_" + id, 1),
            ("Clear", "ms_clear_" + id, 2));
        if (action == 0 && !string.IsNullOrWhiteSpace(input))
        {
            addEntry(input);
            input = "";
        }
        else if (action == 1)
        {
            targetList.Clear();
            input = "";
        }

        var after = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, after.Y));
        ImGui.TextColored(UiHelpers.Muted, $"{targetList.Count} in list");
        for (var i = 0; i < targetList.Count; i++)
        {
            var skillColor = GetSkillEntryColor(targetList[i], Settings.DefaultSkillColor.Value);
            ImGui.PushStyleColor(ImGuiCol.Text, ToImGuiColor(entryColor));
            ImGui.Text(ReferenceEquals(targetList, Settings.SkillFilter) ? "+" : "-");
            ImGui.PopStyleColor();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, ToImGuiColor(skillColor));
            ImGui.Text(targetList[i]);
            ImGui.PopStyleColor();
            ImGui.SameLine();
            if (UiHelpers.Button("x", "ms_del_" + id + "_" + i, 0))
            {
                targetList.RemoveAt(i);
                i--;
            }
        }
    }

    private void DrawClearStrategyPopup()
    {
        if (_clearStrategyOpen) ImGui.OpenPopup("Clear saved data?");
        if (!ImGui.BeginPopupModal("Clear saved data?", ref _clearStrategyOpen, ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextWrapped("Clears your skill lists, tier ranks, support ratings and combo overrides.");
        var choice = UiHelpers.ButtonRow(("Clear", "ms_clearData", 2), ("Cancel", "ms_cancelClear", 0));
        if (choice == 0)
        {
            ClearAllStrategyData();
            _clearStrategyOpen = false;
            ImGui.CloseCurrentPopup();
        }
        else if (choice == 1)
        {
            _clearStrategyOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private void DrawLoadStrategyPopup()
    {
        if (_loadStrategyOpen) ImGui.OpenPopup("Load preset?");
        if (!ImGui.BeginPopupModal("Load preset?", ref _loadStrategyOpen, ImGuiWindowFlags.AlwaysAutoResize)) return;
        ImGui.TextWrapped("Replaces your skill lists, tier ranks, support ratings and combo overrides with the preset.");
        var choice = UiHelpers.ButtonRow(("Apply preset", "ms_applyStrat", 1), ("Cancel", "ms_cancelStrat", 0));
        if (choice == 0)
        {
            LoadAutoAssignedStrategy();
            _loadStrategyOpen = false;
            ImGui.CloseCurrentPopup();
        }
        else if (choice == 1)
        {
            _loadStrategyOpen = false;
            ImGui.CloseCurrentPopup();
        }
        ImGui.EndPopup();
    }

    private static readonly string[] TierLabels = ["None", "S", "A", "B", "C"];

    private void DrawTierDropdowns()
    {
        var allNames = Settings.MercenaryTiers.Keys.OrderBy(x => x).ToList();
        foreach (var group in StatGroups)
        {
            var groupNames = allNames
                .Where(n => MercenaryStats.TryGetValue(n, out var s) && s.SequenceEqual(group.Filter))
                .ToList();
            if (groupNames.Count == 0) continue;

            ImGui.PushStyleColor(ImGuiCol.Text, group.Color);
            UiHelpers.SectionHeader(group.Label);
            ImGui.PopStyleColor();

            foreach (var name in groupNames)
            {
                var value = Settings.MercenaryTiers[name];
                var rowStart = ImGui.GetCursorScreenPos();
                float rowLineH = ImGui.GetTextLineHeight();
                ImGui.SetCursorScreenPos(rowStart + new Vector2(44f, 0f));
                ImGui.Text(name);
                ImGui.SetCursorScreenPos(rowStart + new Vector2(270f, 0f));
                ImGui.SetNextItemWidth(90f);
                if (ImGui.BeginCombo("##ms_tier_" + name, TierLabels[Math.Clamp(value, 0, TierLabels.Length - 1)]))
                {
                    for (var i = 0; i < TierLabels.Length; i++)
                        if (ImGui.Selectable(TierLabels[i], i == value))
                            Settings.MercenaryTiers[name] = i;
                    ImGui.EndCombo();
                }
                ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + rowLineH + 8f));
            }
        }
    }

    private static Vector4 ToImGuiColor(SharpDX.Color color) =>
        new(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);

    private SharpDX.Color GetSkillEntryColor(string entry, SharpDX.Color fallback)
    {
        if (_skillAttributeMap == null || string.IsNullOrWhiteSpace(entry)) return fallback;
        var compact = entry.Replace(" ", "", StringComparison.Ordinal);
        if (!_skillAttributeMap.TryGetValue(entry, out var attribute))
            _skillAttributeMap.TryGetValue(compact, out attribute);
        if (attribute.HasValue) return GetColorForStat(attribute);
        foreach (var (skillName, skillAttribute) in _skillAttributeMap)
        {
            if (!skillAttribute.HasValue) continue;
            if (skillName.Contains(entry, StringComparison.OrdinalIgnoreCase) ||
                entry.Contains(skillName, StringComparison.OrdinalIgnoreCase))
                return GetColorForStat(skillAttribute);
        }
        return fallback;
    }

    private void ResetSkillColors()
    {
        Settings.HighlightSkillColor.Value = new SharpDX.Color(124, 252, 0, 255);
        Settings.BadSkillColor.Value = new SharpDX.Color(255, 0, 0, 255);
        Settings.DefaultSkillColor.Value = new SharpDX.Color(255, 255, 255, 255);
        Settings.MonsterSkillColor.Value = new SharpDX.Color(147, 112, 219, 255);
        Settings.AuraActiveColor.Value = new SharpDX.Color(0, 255, 255, 255);
        Settings.AuraInactiveColor.Value = new SharpDX.Color(105, 105, 105, 255);
        Settings.BackgroundColor.Value = new SharpDX.Color(0, 0, 0, 255);
    }

    private void DrawFlameLinkTab()
    {
        UiHelpers.SectionHeader("Link status");
        Settings.ShowLinkStatus.Value = ToggleRow("Show link status", Settings.ShowLinkStatus, "linkStatus",
            "Shows the link state and the reason when it breaks.");
        Settings.AutoCastFlameLink.Value = ToggleRow("Auto-cast Flame Link", Settings.AutoCastFlameLink, "autoFlame",
            "Casts Flame Link when the link drops.");
        var keyStart = ImGui.GetCursorScreenPos();
        // Button text starts 10px into the button, so x=34 puts it on the 44px label gutter.
        ImGui.SetCursorScreenPos(keyStart + new Vector2(34f, 0f));
        ThemedHotkeyButton(Settings.FlameLinkKey, $"Link key [{Settings.FlameLinkKey.Value}]", "ms_flameKey");
        ImGui.SetCursorScreenPos(new Vector2(keyStart.X, keyStart.Y + ImGui.GetTextLineHeight() + 8f));
        Settings.RequireSkillReady.Value = ToggleRow("Wait for skill to be ready", Settings.RequireSkillReady, "skillReady",
            "Waits for the skill cooldown before casting.");
        Settings.RestoreCursor.Value = ToggleRow("Restore cursor", Settings.RestoreCursor, "restoreCursor",
            "Puts the mouse back where it was after casting.");
        DrawFlameLinkStatus();

        if (!_showAdvancedSettings) return;

        UiHelpers.SectionHeader("Colors");
        ColorRow("Linked color", Settings.LinkedColor);

        UiHelpers.SectionHeader("Cast settings");
        ImGui.BeginDisabled(!Settings.AutoCastFlameLink.Value);
        SliderRow(Settings.CursorSettleMs, "Cursor settle (ms)", "Time to wait after moving the mouse.");
        SliderRow(Settings.CursorRestoreMs, "Cursor restore (ms)", "Delay before moving the mouse back.");
        SliderRow(Settings.CastMarginMs, "Cast margin (ms)", "Extra time before the skill comes off cooldown.");
        SliderRow(Settings.CastGapMs, "Gap fallback (ms)", "Gap to use when the real one cannot be read.");
        SliderRow(Settings.RelinkCooldownMs, "Relink cooldown (ms)", "Minimum time between casts.");
        SliderRow(Settings.MaxCastDistance, "Max cast distance (m)", "Skip casting beyond this range.");
        Settings.DontCastInTown.Value = ToggleRow("Don't cast in town", Settings.DontCastInTown, "noTown",
            "Blocks auto-cast while you are in town.");
        Settings.DontCastWithPanelsOpen.Value = ToggleRow("Don't cast with panels open", Settings.DontCastWithPanelsOpen, "noPanels",
            "Blocks auto-cast while a UI panel is open.");
        ImGui.EndDisabled();
    }

    private void DrawFlameLinkStatus()
    {
        if (_flameLink == null) ImGui.Text("Status: not ready");
        else ImGui.Text($"Status: {_flameLink.BlockedReason} | gap: {_flameLink.EffectiveGapMs} ms | casts: {_flameLink.CastCount}");
    }
}