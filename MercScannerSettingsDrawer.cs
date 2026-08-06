using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using SharpDX;
using static MercScanner.MercScanner;

namespace MercScanner;

public class MercScannerSettingsDrawer
{
    private readonly MercScannerSettings _settings;
    private readonly MercScanner _plugin;
    private int _selectedTab;
    private string _activeSliderEditId;
    private readonly Dictionary<string, string> _sliderBuffers = new();
    private int _selectedAuraIndex = -1;

    private static readonly (string Label, System.Numerics.Vector4 Color, List<StatType> Filter)[] StatGroups =
    [
        ("Strength",              new(0.82f, 0.12f, 0.12f, 1), [StatType.Str]),
        ("Dexterity",             new(0.12f, 0.82f, 0.12f, 1), [StatType.Dex]),
        ("Intelligence",          new(0.12f, 0.50f, 1.00f, 1), [StatType.Int]),
        ("Str / Dex",             new(0.82f, 0.50f, 0.00f, 1), [StatType.Str, StatType.Dex]),
        ("Str / Int",             new(0.82f, 0.30f, 0.60f, 1), [StatType.Str, StatType.Int]),
        ("Dex / Int",             new(0.00f, 0.65f, 0.50f, 1), [StatType.Dex, StatType.Int]),
        ("Str / Dex / Int",       new(0.60f, 0.30f, 0.60f, 1), [StatType.Str, StatType.Dex, StatType.Int]),
    ];

    private static readonly (string Label, System.Numerics.Vector4 Color)[] TabData =
    [
        ("Tiers",          new(0.50f, 0.80f, 1.00f, 1f)),
        ("Unhired Mercs",  new(0.40f, 0.75f, 0.95f, 1f)),
        ("Frames",         new(0.50f, 0.70f, 0.90f, 1f)),
        ("Skills & Auras", new(0.45f, 0.65f, 0.85f, 1f)),
        ("Hired Mercs",    new(0.30f, 1.00f, 0.60f, 1f)),
        ("Flame Link",     new(1.00f, 0.60f, 0.30f, 1f)),
    ];

    private static readonly string[] KnownAuraDisplayNames =
        MercScanner.KnownAuraEntries.Select(e => e.DisplayName).ToArray();

    public MercScannerSettingsDrawer(MercScanner plugin, MercScannerSettings settings)
    {
        _plugin = plugin;
        _settings = settings;
    }

    public void Draw()
    {
        PushWindowStyle();
        try
        {
            DrawGeneralSettings();
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            var sidebarHeight = ImGui.GetContentRegionAvail().Y;
            if (ImGui.BeginChild("Sidebar", new System.Numerics.Vector2(140f, sidebarHeight), ImGuiChildFlags.Border, ImGuiWindowFlags.None))
            {
                for (var i = 0; i < TabData.Length; i++)
                    DrawTabSelector(TabData[i].Label, i, TabData[i].Color);
            }
            ImGui.EndChild();

            ImGui.SameLine();

            if (ImGui.BeginChild("Content", new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X - 4f, sidebarHeight), ImGuiChildFlags.Border, ImGuiWindowFlags.None))
            {
                switch (_selectedTab)
                {
                    case 0: DrawTiersTab(); break;
                    case 1: DrawUnhiredMercsTab(); break;
                    case 2: DrawFramesTab(); break;
                    case 3: DrawSkillsAurasTab(); break;
                    case 4: DrawHiredTab(); break;
                    case 5: DrawFlameLinkTab(); break;
                }
            }
            ImGui.EndChild();
        }
        finally
        {
            ImGui.PopStyleColor(20);
            ImGui.PopStyleVar(9);
        }
    }

    private void DrawTabSelector(string label, int index, System.Numerics.Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        if (ImGui.Selectable(label, _selectedTab == index))
            _selectedTab = index;
        ImGui.PopStyleColor();
    }

    private void DrawGeneralSettings()
    {
        ImGui.Text("General");
        ImGui.Separator();
        DrawToggleNode("Master Enable", _settings.Enable);
        ImGui.Spacing();
        ImGui.Text("Areas");
        ImGui.Separator();
        DrawToggleNode("Ignore Fullscreen Panels", _settings.IgnoreFullscreenPanels);
        DrawToggleNode("Ignore Large Panels", _settings.IgnoreLargePanels);
    }

    private void DrawTiersTab()
    {
        DrawToggleNode("Show Tier Label", _settings.ShowTierText);
        ImGui.Spacing();
        DrawColorNode("S Tier", _settings.STierColor);
        DrawColorNode("A Tier", _settings.ATierColor);
        DrawColorNode("B Tier", _settings.BTierColor);
        DrawColorNode("C Tier", _settings.CTierColor);

        if (ImGui.Button("Auto Assign Tiers From Live Data"))
            _plugin.AutoAssignTiers();
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Score visible mercenaries from their live stats and assign S/A/B/C automatically. Overwrites manual tiers.");

        ImGui.Separator();
        ImGui.TextDisabled("0=None  1=S  2=A  3=B  4=C");
        ImGui.Separator();

        ImGui.Spacing();
        ImGui.Text("Tier Frame");
        ImGui.Separator();
        DrawToggleNode("Show Fill Background", _settings.ShowTierFrame);
        if (_settings.ShowTierFrame.Value)
        {
            var opacity = _settings.TierFrameFillOpacity.Value;
            if (DrawIntSlider("Fill Opacity", ref opacity, 0, 100))
                _settings.TierFrameFillOpacity.Value = opacity;
        }

        ImGui.Spacing();
        DrawToggleNode("Show Snake Effect (S tier only)", _settings.ShowTierSnake);
        if (_settings.ShowTierSnake.Value)
        {
            var speed = _settings.TierSnakeSpeed.Value;
            if (ImGui.SliderFloat("Speed", ref speed, _settings.TierSnakeSpeed.Min, _settings.TierSnakeSpeed.Max))
                _settings.TierSnakeSpeed.Value = speed;

            var intensity = _settings.TierSnakeIntensity.Value;
            if (ImGui.SliderFloat("Intensity", ref intensity, _settings.TierSnakeIntensity.Min, _settings.TierSnakeIntensity.Max))
                _settings.TierSnakeIntensity.Value = intensity;
        }

        ImGui.Separator();

        var allNames = _settings.MercenaryTiers.Keys.OrderBy(x => x).ToList();

        foreach (var group in StatGroups)
        {
            var groupNames = allNames
                .Where(n => MercenaryStats.TryGetValue(n, out var s) && s.SequenceEqual(group.Filter))
                .ToList();
            if (groupNames.Count == 0) continue;

            ImGui.PushStyleColor(ImGuiCol.Text, group.Color);
            ImGui.Separator();
            ImGui.Text(group.Label);
            ImGui.Separator();
            ImGui.PopStyleColor();

            foreach (var name in groupNames)
            {
                var value = _settings.MercenaryTiers[name];
                if (DrawIntSlider(name, ref value, 0, 4))
                    _settings.MercenaryTiers[name] = value;
            }
        }
    }

    private void DrawUnhiredMercsTab()
    {
        DrawToggleNode("Enable Entity Overlays", _settings.ShowEntityOverlays);
        ImGui.Spacing();
        DrawToggleNode("Show HP Bar", _settings.ShowHpBar);
        ImGui.Spacing();
        DrawColorNode("HP Bar Color", _settings.HpBarColor);
        ImGui.Spacing();
        DrawToggleNode("Show Stat Rings", _settings.ShowEntityFrames);
        DrawToggleNode("Show Tier Label", _settings.ShowEntityTier);
        ImGui.Spacing();
        DrawToggleNode("Infer Stats From Live Data", _settings.UseLiveStatInference);
        DrawToggleNode("Show Level", _settings.ShowMercLevel);
        ImGui.Spacing();

        var dist = _settings.MaxMercDistance.Value;
        if (DrawIntSlider("Max Distance", ref dist, 10, 200))
            _settings.MaxMercDistance.Value = dist;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("Off-Screen Indicators");
        ImGui.Separator();
        DrawToggleNode("Off-Screen Indicators", _settings.ShowOffScreenIndicators);
        if (_settings.ShowOffScreenIndicators.Value)
        {
            var indicatorDist = _settings.MaxIndicatorDistance.Value;
            if (DrawIntSlider("Indicator Distance", ref indicatorDist, _settings.MaxIndicatorDistance.Min, _settings.MaxIndicatorDistance.Max))
                _settings.MaxIndicatorDistance.Value = indicatorDist;
        }
    }

    private void DrawFramesTab()
    {
        DrawToggleNode("Highlight Mercenary Items", _settings.HighlightMercenary);
        ImGui.Spacing();
        DrawColorNode("Strength Color", _settings.StrColor);
        DrawColorNode("Dexterity Color", _settings.DexColor);
        DrawColorNode("Intelligence Color", _settings.IntColor);
    }

    private void DrawSkillsAurasTab()
    {
        if (ImGui.CollapsingHeader("Skills", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawToggleNode("Show All Skills", _settings.ShowAllSkills);
            ImGui.Spacing();
            DrawColorNode("Highlight Color", _settings.HighlightSkillColor);
            DrawColorNode("Default Color", _settings.DefaultSkillColor);
            DrawColorNode("Monster Skill Color", _settings.MonsterSkillColor);

            ImGui.Spacing();
            if (ImGui.CollapsingHeader("Filters", ImGuiTreeNodeFlags.DefaultOpen))
            {
                DrawFilterQuickAdd();
                ImGui.Separator();
                DrawContentNode("Aura & Skill Filter", _settings.SkillFilter);
            }
        }

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Auras", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawToggleNode("Separate Aura Display", _settings.SeparateAuraDisplay);
            DrawToggleNode("Auto-Detect Known Auras", _settings.AutoDetectAuras);
            DrawToggleNode("Show Aura Timers", _settings.ShowAuraTimers);
            ImGui.Spacing();
            DrawColorNode("Active Aura Color", _settings.AuraActiveColor);
            DrawColorNode("Inactive Aura Color", _settings.AuraInactiveColor);
        }

        ImGui.Separator();
        DrawColorNode("Background Color", _settings.BackgroundColor);
    }

    private void DrawFilterQuickAdd()
    {
        ImGui.Text("Quick Add Aura");
        ImGui.SameLine();
        ImGui.PushItemWidth(180f);

        var preview = _selectedAuraIndex >= 0 && _selectedAuraIndex < KnownAuraDisplayNames.Length
            ? KnownAuraDisplayNames[_selectedAuraIndex]
            : "Select...";

        if (ImGui.BeginCombo("##auraQuickAdd", preview))
        {
            for (var i = 0; i < KnownAuraDisplayNames.Length; i++)
            {
                var isSelected = _selectedAuraIndex == i;
                if (ImGui.Selectable(KnownAuraDisplayNames[i], isSelected))
                {
                    _selectedAuraIndex = i;
                    AddAuraToFilter(KnownAuraDisplayNames[i]);
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (ImGui.Button("Add +") && _selectedAuraIndex >= 0 && _selectedAuraIndex < KnownAuraDisplayNames.Length)
        {
            AddAuraToFilter(KnownAuraDisplayNames[_selectedAuraIndex]);
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Add the selected aura to your filter list");

        ImGui.SameLine();
        if (ImGui.Button("Add All"))
        {
            foreach (var (_, displayName) in KnownAuraEntries)
                AddAuraToFilter(displayName);
        }

        ImGui.Spacing();
        if (ImGui.Button("Clear All"))
        {
            _settings.SkillFilter.Content.Clear();
        }
        if (ImGui.IsItemHovered()) ImGui.SetTooltip("Remove all filter entries");
    }

    private void AddAuraToFilter(string displayName)
    {
        if (_settings.SkillFilter.Content.Any(x =>
                x.Value.Equals(displayName, StringComparison.InvariantCultureIgnoreCase))) return;
        _settings.SkillFilter.Content.Add(new TextNode(displayName));
    }

    private void DrawToggleNode(string label, ToggleNode node)
    {
        var value = node.Value;
        if (ImGui.Checkbox(label, ref value))
            node.Value = value;
    }

    private void DrawColorNode(string label, ColorNode node)
    {
        var sd = node.Value.ToVector4();
        var v = new System.Numerics.Vector4(sd.X, sd.Y, sd.Z, sd.W);
        if (ImGui.ColorEdit4(label, ref v, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
            node.Value = new Color(v.X, v.Y, v.Z, v.W);
    }

    private void DrawHiredTab()
    {
        DrawToggleNode("Enable Hired Merc Overlays", _settings.ShowHiredMercOverlays);
        ImGui.Spacing();
        DrawToggleNode("Show HP Bar", _settings.ShowHiredMercHpBar);
        ImGui.Spacing();
        DrawToggleNode("Show Action Panel", _settings.ShowHiredMercActionPanel);
        DrawToggleNode("Show Skill Cooldowns", _settings.ShowSkillCooldowns);
        if (_settings.ShowSkillCooldowns.Value)
        {
            var style = _settings.SkillCooldownBarStyle.Value;
            if (DrawBarStyleCombo(ref style))
                _settings.SkillCooldownBarStyle.Value = style;
        }
        DrawToggleNode("Show Filtered Buffs", _settings.ShowHiredMercBuffs);
    }

    private void DrawFlameLinkTab()
    {
        if (ImGui.CollapsingHeader("Link Status", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawToggleNode("Show Link Status", _settings.ShowLinkStatus);
            DrawColorNode("Linked Color", _settings.LinkedColor);
            DrawColorNode("Unlinked Color", _settings.UnlinkedColor);
        }

        ImGui.Spacing();
        if (ImGui.CollapsingHeader("Auto-Cast", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawToggleNode("Auto-Cast Flame Link", _settings.AutoCastFlameLink);
            if (_settings.AutoCastFlameLink.Value)
            {
                ImGui.Spacing();
                _settings.FlameLinkKey.DrawPickerButton("Link Key");
                DrawToggleNode("Wait For Skill Ready", _settings.RequireSkillReady);
                DrawToggleNode("Restore Cursor", _settings.RestoreCursor);

                ImGui.Spacing();
                var settle = _settings.CursorSettleMs.Value;
                if (DrawIntSlider("Cursor Settle (ms)", ref settle, _settings.CursorSettleMs.Min, _settings.CursorSettleMs.Max))
                    _settings.CursorSettleMs.Value = settle;

                var restore = _settings.CursorRestoreMs.Value;
                if (DrawIntSlider("Cursor Restore (ms)", ref restore, _settings.CursorRestoreMs.Min, _settings.CursorRestoreMs.Max))
                    _settings.CursorRestoreMs.Value = restore;

                var margin = _settings.CastMarginMs.Value;
                if (DrawIntSlider("Cast Margin (ms)", ref margin, _settings.CastMarginMs.Min, _settings.CastMarginMs.Max))
                    _settings.CastMarginMs.Value = margin;

                var gap = _settings.CastGapMs.Value;
                if (DrawIntSlider("Cast Gap Fallback (ms)", ref gap, _settings.CastGapMs.Min, _settings.CastGapMs.Max))
                    _settings.CastGapMs.Value = gap;

                var distance = _settings.MaxCastDistance.Value;
                if (DrawFloatSlider("Max Cast Distance", ref distance, _settings.MaxCastDistance.Min, _settings.MaxCastDistance.Max))
                    _settings.MaxCastDistance.Value = distance;

                ImGui.Spacing();
                DrawToggleNode("Never Cast In Town", _settings.DontCastInTown);
                DrawToggleNode("Never Cast With Panels Open", _settings.DontCastWithPanelsOpen);

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextWrapped(_plugin.FlameLink != null
                    ? $"Status: {_plugin.FlameLink.BlockedReason} | gap: {_plugin.FlameLink.CastGapMs} ms | casts: {_plugin.FlameLink.CastCount}"
                    : "Status: not initialized");
            }
        }
    }

    private static readonly string[] BarStyleLabels = ["Inline", "Below Text", "Fill Background"];

    private bool DrawBarStyleCombo(ref int style)
    {
        var preview = BarStyleLabels[Math.Clamp(style, 0, BarStyleLabels.Length - 1)];
        ImGui.Text("Cooldown Bar Style");
        ImGui.SameLine();
        ImGui.PushItemWidth(180f);
        var changed = false;
        if (ImGui.BeginCombo("##cdBarStyle", preview))
        {
            for (var i = 0; i < BarStyleLabels.Length; i++)
            {
                var isSelected = style == i;
                if (ImGui.Selectable(BarStyleLabels[i], isSelected))
                {
                    style = i;
                    changed = true;
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
        ImGui.PopItemWidth();
        return changed;
    }

    private void DrawContentNode(string label, ContentNode<TextNode> node)
    {
        ImGui.Text(label);
        for (var i = node.Content.Count - 1; i >= 0; i--)
        {
            ImGui.PushID($"{label}_{i}");
            if (ImGui.Button("Remove"))
            {
                node.Content.RemoveAt(i);
            }
            else
            {
                ImGui.SameLine();
                var val = node.Content[i].Value;
                if (ImGui.InputText($"##{label}Text", ref val, 100))
                    node.Content[i].Value = val;
            }
            ImGui.PopID();
        }

        if (ImGui.Button($"Add item##{label}Add"))
            node.Content.Add(new TextNode(""));
    }

    private bool DrawFloatSlider(string id, ref float value, float min, float max)
    {
        return DrawSliderCore(id, ref value, min, max, out _);
    }

    private bool DrawIntSlider(string id, ref int value, int min, int max)
    {
        if (_activeSliderEditId == id)
            return HandleSliderTextInput(id, ref value, min, max);

        var floatValue = (float)value;
        if (!DrawSliderCore(id, ref floatValue, min, max, out var valueClicked))
        {
            if (valueClicked)
            {
                _activeSliderEditId = id;
                _sliderBuffers[id] = value.ToString();
            }
            return false;
        }

        value = (int)Math.Round(floatValue);
        return true;
    }

    private bool HandleSliderTextInput(string id, ref int value, int min, int max)
    {
        if (!_sliderBuffers.TryGetValue(id, out var buffer))
        {
            buffer = value.ToString();
            _sliderBuffers[id] = buffer;
        }

        ImGui.SetKeyboardFocusHere();
        if (ImGui.InputText($"##{id}_edit", ref buffer, 10, ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
        {
            if (int.TryParse(buffer, out var newValue))
            {
                value = Math.Clamp(newValue, min, max);
            }
            _activeSliderEditId = null;
            _sliderBuffers.Remove(id);
            return true;
        }

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsItemHovered())
        {
            _activeSliderEditId = null;
            _sliderBuffers.Remove(id);
        }

        _sliderBuffers[id] = buffer;
        return false;
    }

    private static uint PackColor(float r, float g, float b, float a)
    {
        return (uint)(a * 255) << 24 | (uint)(b * 255) << 16 | (uint)(g * 255) << 8 | (uint)(r * 255);
    }

    private bool DrawSliderCore(string id, ref float value, float min, float max, out bool valueClicked)
    {
        valueClicked = false;
        var labelSize = ImGui.CalcTextSize(id);
        var valueText = ((int)value).ToString();
        var valueSize = ImGui.CalcTextSize(valueText);
        var height = Math.Max(labelSize.Y, valueSize.Y) + 6f;
        var cursor = ImGui.GetCursorScreenPos();
        var totalWidth = ImGui.GetContentRegionAvail().X;

        var labelPosition = cursor;
        var valuePosition = new System.Numerics.Vector2(cursor.X + totalWidth - valueSize.X, cursor.Y + (height - valueSize.Y) * 0.5f);
        var lineStartX = cursor.X + labelSize.X + 12f;
        var lineEndX = cursor.X + totalWidth - valueSize.X - 12f;
        var lineLength = lineEndX - lineStartX;
        var lineY = cursor.Y + height * 0.5f;

        var valueRectMin = valuePosition;
        var valueRectMax = new System.Numerics.Vector2(valuePosition.X + valueSize.X, valuePosition.Y + valueSize.Y);

        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            var mousePos = ImGui.GetMousePos();
            if (mousePos.X >= valueRectMin.X && mousePos.X <= valueRectMax.X &&
                mousePos.Y >= valueRectMin.Y && mousePos.Y <= valueRectMax.Y)
            {
                valueClicked = true;
            }
        }

        var sliderButtonWidth = totalWidth;
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0f, height + 2f));
        ImGui.PushStyleColor(ImGuiCol.Button, 0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
        ImGui.PushStyleColor(ImGuiCol.Text, 0);
        var clicked = ImGui.InvisibleButton($"##{id}", new System.Numerics.Vector2(sliderButtonWidth, height));
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar();

        if (lineLength <= 0f || max <= min)
            return false;

        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(labelPosition, PackColor(0.70f, 0.85f, 1.00f, 1f), id);
        drawList.AddText(valuePosition, PackColor(0.40f, 0.90f, 1.00f, 1f), valueText);

        var normalized = Math.Clamp((value - min) / (max - min), 0f, 1f);
        var dotX = lineStartX + normalized * lineLength;

        drawList.AddLine(new System.Numerics.Vector2(lineStartX, lineY), new System.Numerics.Vector2(lineEndX, lineY), PackColor(0.08f, 0.25f, 0.40f, 1f), 2f);
        drawList.AddLine(new System.Numerics.Vector2(lineStartX, lineY), new System.Numerics.Vector2(dotX, lineY), PackColor(0.20f, 0.75f, 0.95f, 0.7f), 2f);

        var isActive = ImGui.IsItemActive();
        var isHovered = ImGui.IsItemHovered();
        var dotRadius = isActive ? 7f : isHovered ? 6.5f : 5.5f;
        var dotColor = isActive
            ? PackColor(0.40f, 0.90f, 1.00f, 1.0f)
            : isHovered
                ? PackColor(0.30f, 0.80f, 0.95f, 1.0f)
                : PackColor(0.20f, 0.70f, 0.90f, 1.0f);

        drawList.AddCircleFilled(new System.Numerics.Vector2(dotX, lineY), dotRadius, dotColor);
        drawList.AddCircleFilled(new System.Numerics.Vector2(dotX, lineY), dotRadius - 2f, PackColor(0.02f, 0.08f, 0.15f, 1f));
        drawList.AddCircleFilled(new System.Numerics.Vector2(dotX, lineY), dotRadius - 3.5f, dotColor);

        if (!valueClicked && (isActive || (clicked && isHovered)))
        {
            var mousePosition = ImGui.GetMousePos();
            var newNormalized = Math.Clamp((mousePosition.X - lineStartX) / lineLength, 0f, 1f);
            value = Math.Clamp(min + newNormalized * (max - min), min, max);
            return true;
        }

        return false;
    }

    private static void PushWindowStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new System.Numerics.Vector2(10f, 10f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(6f, 3f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(8f, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 3f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 3f);

        ImGui.PushStyleColor(ImGuiCol.WindowBg, new System.Numerics.Vector4(0.02f, 0.08f, 0.15f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new System.Numerics.Vector4(0.03f, 0.10f, 0.18f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Border, new System.Numerics.Vector4(0.20f, 0.50f, 0.70f, 0.50f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new System.Numerics.Vector4(0.06f, 0.18f, 0.30f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new System.Numerics.Vector4(0.10f, 0.30f, 0.50f, 1f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new System.Numerics.Vector4(0.15f, 0.40f, 0.65f, 1f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, new System.Numerics.Vector4(0.40f, 0.85f, 1.00f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, new System.Numerics.Vector4(0.20f, 0.70f, 0.90f, 1f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, new System.Numerics.Vector4(0.40f, 0.85f, 1.00f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.08f, 0.25f, 0.45f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.15f, 0.40f, 0.65f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.25f, 0.55f, 0.80f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Header, new System.Numerics.Vector4(0.08f, 0.30f, 0.55f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new System.Numerics.Vector4(0.15f, 0.45f, 0.75f, 1f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new System.Numerics.Vector4(0.25f, 0.60f, 0.90f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Separator, new System.Numerics.Vector4(0.20f, 0.45f, 0.65f, 0.40f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, new System.Numerics.Vector4(0.03f, 0.08f, 0.12f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, new System.Numerics.Vector4(0.20f, 0.50f, 0.70f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, new System.Numerics.Vector4(0.35f, 0.65f, 0.85f, 1f));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, new System.Numerics.Vector4(0.50f, 0.80f, 1.00f, 1f));
    }
}
