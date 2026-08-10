using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ExileCore.Shared.Nodes;
using ImGuiNET;

namespace MercScanner;

public partial class MercScanner
{
    public override void DrawSettings()
    {
        if (!ImGui.BeginTabBar("##mercTabs")) return;

        (string, Action)[] tabs =
        {
            ("General", DrawGeneralTab),
            ("Tiers", DrawTiersTab),
            ("Unhired Mercs", DrawUnhiredTab),
            ("Labels", DrawLabelsTab),
            ("Skills & Auras", DrawSkillsTab),
            ("Hired Mercs", DrawHiredTab),
            ("Flame Link", DrawFlameLinkTab),
        };

        foreach (var (label, draw) in tabs)
            if (ImGui.BeginTabItem(label)) { draw(); ImGui.EndTabItem(); }

        ImGui.EndTabBar();
    }

    private static void DrawToggle(string label, ToggleNode node)
    {
        var v = node.Value;
        if (ImGui.Checkbox(label, ref v)) node.Value = v;
    }

    private static void DrawColor(string label, ColorNode node)
    {
        var c = node.Value;
        var v = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
        if (ImGui.ColorEdit4(label, ref v))
            node.Value = new SharpDX.Color((byte)(v.X * 255f), (byte)(v.Y * 255f), (byte)(v.Z * 255f), (byte)(v.W * 255f));
    }

    private static void DrawSlider(string label, RangeNode<int> node)
    {
        var v = node.Value;
        if (ImGui.SliderInt(label, ref v, node.Min, node.Max)) node.Value = v;
    }

    private static void DrawSlider(string label, RangeNode<float> node)
    {
        var v = node.Value;
        if (ImGui.SliderFloat(label, ref v, node.Min, node.Max)) node.Value = v;
    }

    private static void Section(string title) => ImGui.SeparatorText(title);

    private void DrawGeneralTab()
    {
        DrawToggle("Enable Plugin", Settings.Enable);
        Section("Panels");
        DrawToggle("Ignore Fullscreen Panels", Settings.IgnoreFullscreenPanels);
        DrawToggle("Ignore Large Panels", Settings.IgnoreLargePanels);
    }

    private void DrawTiersTab()
    {
        DrawToggle("Show Tier Label", Settings.ShowTierText);
        DrawColor("S Tier", Settings.STierColor);
        DrawColor("A Tier", Settings.ATierColor);
        DrawColor("B Tier", Settings.BTierColor);
        DrawColor("C Tier", Settings.CTierColor);

        Section("Auto-Assigned Strategy");
        if (ImGui.Button("Load Auto-Assigned Strategy"))
            LoadAutoAssignedStrategy();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Populate the wanted/bad skill lists, archetype tiers and support ratings from the curated 3.29 meta preset. Replaces current lists.");
        ImGui.SameLine();
        if (ImGui.Button("Restore Defaults"))
            ZeroAllDefaults();
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Factory reset: clear the skill lists, zero every archetype tier, and wipe all support ratings.");

        Section("Mercenary Tiers");
        DrawTierDropdowns();

        Section("Tier Frame");
        DrawToggle("Show Fill Background", Settings.ShowTierFrame);
        DrawSlider("Fill Opacity", Settings.TierFrameFillOpacity);
        DrawToggle("Show Snake Effect (S tier only)", Settings.ShowTierSnake);
        DrawSlider("Snake Speed", Settings.TierSnakeSpeed);
        DrawSlider("Snake Intensity", Settings.TierSnakeIntensity);
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
            ImGui.Separator();
            ImGui.Text(group.Label);
            ImGui.Separator();
            ImGui.PopStyleColor();

            foreach (var name in groupNames)
            {
                var value = Settings.MercenaryTiers[name];
                var preview = TierLabels[Math.Clamp(value, 0, TierLabels.Length - 1)];
                ImGui.SetNextItemWidth(90f);
                if (ImGui.BeginCombo($"##tier_{name}", preview))
                {
                    for (var i = 0; i < TierLabels.Length; i++)
                    {
                        if (ImGui.Selectable(TierLabels[i], i == value))
                            Settings.MercenaryTiers[name] = i;
                    }
                    ImGui.EndCombo();
                }
                ImGui.SameLine();
                ImGui.Text(name);
            }
        }
    }

    private void DrawUnhiredTab()
    {
        DrawToggle("Enable Entity Overlays", Settings.ShowEntityOverlays);
        DrawToggle("Show HP Bar", Settings.ShowHpBar);
        DrawColor("HP Bar Color", Settings.HpBarColor);
        DrawToggle("Show Stat Rings", Settings.ShowEntityFrames);
        DrawToggle("Show Tier Label", Settings.ShowEntityTier);
        DrawToggle("Infer Stats From Live Data", Settings.UseLiveStatInference);
        DrawToggle("Show Level", Settings.ShowMercLevel);
        DrawSlider("Max Distance", Settings.MaxMercDistance);
        DrawToggle("Off-Screen Indicators", Settings.ShowOffScreenIndicators);
        DrawSlider("Indicator Distance", Settings.MaxIndicatorDistance);
    }

    private void DrawLabelsTab()
    {
        DrawToggle("Highlight Mercenary Name Labels", Settings.HighlightMercenary);
        DrawColor("Strength Label Color", Settings.StrColor);
        DrawColor("Dexterity Label Color", Settings.DexColor);
        DrawColor("Intelligence Label Color", Settings.IntColor);
    }

    private void DrawSkillsTab()
    {
        DrawToggle("Show All Skills", Settings.ShowAllSkills);
        DrawToggle("Separate Aura Display", Settings.SeparateAuraDisplay);
        DrawToggle("Auto-Detect Known Auras", Settings.AutoDetectAuras);
        DrawToggle("Show Aura Timers", Settings.ShowAuraTimers);
        DrawToggle("Highlight Encounter Panel Skills & Supports", Settings.HighlightEncounterPanelBorders);
        DrawColor("Highlight Color", Settings.HighlightSkillColor);
        DrawColor("Bad Skill Color", Settings.BadSkillColor);
        DrawColor("Default Color", Settings.DefaultSkillColor);
        DrawColor("Monster Skill Color", Settings.MonsterSkillColor);
        DrawColor("Active Aura Color", Settings.AuraActiveColor);
        DrawColor("Inactive Aura Color", Settings.AuraInactiveColor);
        DrawColor("Background Color", Settings.BackgroundColor);

        Section("Wanted Skills (green +)");
        DrawQuickAdd(ref _selectedAuraIndex, Settings.SkillFilter, AddAuraToFilter, "##wantedAura");
        DrawFilterListEditor("##wantedInput", Settings.SkillFilter, ref _skillFilterInput, AddSkillFilterEntry, "Add Skill");

        Section("Bad Skills (red -)");
        DrawQuickAdd(ref _badSelectedAuraIndex, Settings.BadSkillFilter, AddBadAuraToFilter, "##badAura");
        DrawFilterListEditor("##badInput", Settings.BadSkillFilter, ref _badSkillFilterInput, AddBadSkillFilterEntry, "Add Bad Skill");

        Section("Support Ratings (S/A/B/C/D)");
        ImGui.TextWrapped("Support gems in the mercenary offer window get a border and tier letter for how good the support is on the skill it's linked to. Combo-specific ratings override the support's global rating; unrated supports get nothing.");
        foreach (var tier in AllSupportTiers)
        {
            var c = TierColor(tier);
            var pos = ImGui.GetCursorScreenPos();
            ImGui.GetWindowDrawList().AddRectFilled(
                new Vector2(pos.X, pos.Y + 3), new Vector2(pos.X + 14, pos.Y + 17),
                ImGui.GetColorU32(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f)), 2);
            ImGui.SetCursorScreenPos(new Vector2(pos.X + 20, pos.Y));
            ImGui.Text($"{TierLetter(tier)} ({tier})");
        }
        ImGui.Text($"{Settings.SupportRatings.Count} rated supports, {Settings.SupportSkillOverrides.Count} combo overrides.");
    }

    private void DrawQuickAdd(ref int selectedIndex, List<string> targetList, Action<string> addAura, string id)
    {
        ImGui.Text("Quick Add Aura");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180f);

        var preview = selectedIndex >= 0 && selectedIndex < KnownAuraDisplayNames.Length
            ? KnownAuraDisplayNames[selectedIndex]
            : "Select...";

        if (ImGui.BeginCombo(id, preview))
        {
            for (var i = 0; i < KnownAuraDisplayNames.Length; i++)
            {
                var isSelected = selectedIndex == i;
                if (ImGui.Selectable(KnownAuraDisplayNames[i], isSelected))
                {
                    selectedIndex = i;
                    addAura(KnownAuraDisplayNames[i]);
                }
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("Add +" + id) && selectedIndex >= 0 && selectedIndex < KnownAuraDisplayNames.Length)
            addAura(KnownAuraDisplayNames[selectedIndex]);

        ImGui.SameLine();
        if (ImGui.Button("Add All" + id))
        {
            foreach (var (_, displayName) in KnownAuraEntries)
                addAura(displayName);
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear All" + id))
            targetList.Clear();
    }

    private void DrawFilterListEditor(string id, List<string> targetList, ref string input, Action<string> addEntry, string addButtonLabel)
    {
        ImGui.Text("Type Skill Name");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180f);
        if (ImGui.InputText(id, ref input, 64,
                ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.AutoSelectAll))
        {
            addEntry(input);
            input = "";
        }
        ImGui.SameLine();
        if (ImGui.Button(addButtonLabel) && !string.IsNullOrWhiteSpace(input))
        {
            addEntry(input);
            input = "";
        }
        for (var i = 0; i < targetList.Count; i++)
        {
            ImGui.Text(targetList[i]);
            ImGui.SameLine();
            if (ImGui.SmallButton("x" + id + i))
            {
                targetList.RemoveAt(i);
                i--;
            }
        }
    }

    private void DrawHiredTab()
    {
        DrawToggle("Enable Hired Merc Overlays", Settings.ShowHiredMercOverlays);
        DrawToggle("Show HP Bar", Settings.ShowHiredMercHpBar);
        DrawToggle("Show Action Panel", Settings.ShowHiredMercActionPanel);
        DrawToggle("Show Skill Cooldowns", Settings.ShowSkillCooldowns);
        DrawToggle("Show Filtered Buffs", Settings.ShowHiredMercBuffs);

        Section("Cooldown Bar Style");
        var style = Settings.SkillCooldownBarStyle.Value;
        var preview = CooldownBarStyleLabels[Math.Clamp(style, 0, CooldownBarStyleLabels.Length - 1)];
        ImGui.Text("Cooldown Bar Style");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180f);
        if (ImGui.BeginCombo("##cdBarStyle", preview))
        {
            for (var i = 0; i < CooldownBarStyleLabels.Length; i++)
            {
                var isSelected = style == i;
                if (ImGui.Selectable(CooldownBarStyleLabels[i], isSelected))
                    Settings.SkillCooldownBarStyle.Value = i;
                if (isSelected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }

    private void DrawFlameLinkTab()
    {
        DrawToggle("Show Link Status", Settings.ShowLinkStatus);
        DrawToggle("Auto-Cast Flame Link", Settings.AutoCastFlameLink);
        DrawColor("Linked Color", Settings.LinkedColor);
        DrawColor("Unlinked Color", Settings.UnlinkedColor);

        Section("Cast Settings");
        ImGui.Text("Link Key");
        ImGui.SameLine();
        Settings.FlameLinkKey.DrawPickerButton("flamelinkkey");
        DrawToggle("Wait For Skill Ready", Settings.RequireSkillReady);
        DrawToggle("Restore Cursor", Settings.RestoreCursor);
        DrawSlider("Cursor Settle (ms)", Settings.CursorSettleMs);
        DrawSlider("Cursor Restore (ms)", Settings.CursorRestoreMs);
        DrawSlider("Cast Margin (ms)", Settings.CastMarginMs);
        DrawSlider("Cast Gap Fallback (ms)", Settings.CastGapMs);
        DrawSlider("Relink Cooldown (ms)", Settings.RelinkCooldownMs);
        DrawSlider("Max Cast Distance", Settings.MaxCastDistance);
        DrawToggle("Never Cast In Town", Settings.DontCastInTown);
        DrawToggle("Never Cast With Panels Open", Settings.DontCastWithPanelsOpen);

        Section("Auto-Cast Status");
        if (_flameLink == null)
            ImGui.Text("Status: not initialized");
        else
            ImGui.Text($"Status: {_flameLink.BlockedReason} | gap: {_flameLink.EffectiveGapMs} ms | casts: {_flameLink.CastCount}");
    }
}
