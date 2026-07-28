using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.FilesInMemory;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;

namespace MercScanner;

public class MercScanner : BaseSettingsPlugin<MercScannerSettings>
{
    private MercScannerSettingsDrawer _settingsDrawer;
    private Dictionary<string, StatType?> _skillAttributeMap;

    #region Data
    internal enum StatType { Str, Dex, Int }
    internal static readonly Dictionary<string, List<StatType>> MercenaryStats = InitializeMercStats();
    internal static readonly string[] MercenaryKeysSorted = [.. MercenaryStats.Keys.OrderByDescending(k => k.Length)];

    internal static readonly HashSet<string> KnownAuraInternalNames =
    [
        "anger", "wrath", "hatred", "determination", "grace", "discipline",
        "haste", "malevolence", "pride", "zealotry", "clarity", "vitality",
        "purity_of_fire", "purity_of_ice", "purity_of_lightning",
        "envy", "precision",
        "herald_of_ash", "herald_of_ice", "herald_of_thunder",
        "herald_of_agony", "herald_of_purity",
        "aspect_of_the_spider", "aspect_of_the_avian",
        "aspect_of_the_crab", "aspect_of_the_cat",
        "war_banner", "dread_banner", "defiance_banner",
        "arctic_armour",
        "vaal_vitality",
        "summon_skitterbots"
    ];

    #endregion

    #region Helpers
    private static string GetSkillDisplayName(ActorSkill skill)
    {
        var effects = skill.EffectsPerLevel;
        var displayName = effects?.SkillGemWrapper?.ActiveSkill?.DisplayName
                          ?? effects?.SkillGemWrapper?.Name;
        return !string.IsNullOrWhiteSpace(displayName)
            ? (displayName.EndsWith("Mercenary") ? displayName[..^"Mercenary".Length] : displayName)
            : (skill.Name.EndsWith("Mercenary") ? skill.Name[..^"Mercenary".Length] : skill.Name);
    }

    private Buff FindActiveAuraBuff(Buffs buffs, ActorSkill skill, string auraName)
    {
        if (buffs?.BuffsList == null) return null;

        var effects = skill.EffectsPerLevel;

        var byDisplayName = buffs.BuffsList.FirstOrDefault(b =>
        {
            var bn = b.DisplayName ?? b.Name;
            return !string.IsNullOrWhiteSpace(bn) &&
                   bn.Contains(auraName, StringComparison.InvariantCultureIgnoreCase);
        });
        if (byDisplayName != null) return byDisplayName;

        var bySourceSkill = buffs.BuffsList.FirstOrDefault(b =>
        {
            if (b.SourceSkill == null) return false;
            var sn = b.SourceSkill.Name;
            return !string.IsNullOrWhiteSpace(sn) &&
                   sn.Contains(skill.Name, StringComparison.InvariantCultureIgnoreCase);
        });
        if (bySourceSkill != null) return bySourceSkill;

        var byKeyword = buffs.BuffsList.FirstOrDefault(b =>
        {
            var bn = b.DisplayName ?? b.Name;
            return !string.IsNullOrWhiteSpace(bn) &&
                   Settings.SkillFilter.Content.Any(x =>
                       !string.IsNullOrWhiteSpace(x.Value) &&
                       bn.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase));
        });
        return byKeyword;
    }
    #endregion

    private static Dictionary<string, List<StatType>> InitializeMercStats()
    {
        var mercStats = new Dictionary<string, List<StatType>>();
        void AddMercs(IEnumerable<string> names, List<StatType> stats) { foreach (var name in names) mercStats[name] = stats; }

        AddMercs(["Eruptor", "Infamous Eruptor", "Ripper", "Infamous Ripper", "Earthshaker",
            "Infamous Earthshaker", "Smoulderstrike", "Striker", "Infamous Striker"],
            [StatType.Str]);

        AddMercs(["Toxicologist", "Infamous Toxicologist", "Sniper", "Infamous Sniper",
            "Thunderquiver", "Flamequiver", "Infamous Flamequiver", "Manyshot", "Infamous Manyshot"],
            [StatType.Dex]);

        AddMercs(["Stormhand", "Infamous Stormhand", "Frosthand", "Infamous Frosthand",
            "Flamehand", "Withertouch", "Infamous Withertouch", "Reanimator", "Infamous Reanimator",
            "Cruel Mistress", "Infamous Cruel Mistress"],
            [StatType.Int]);

        AddMercs(["Bastion", "Infamous Bastion", "Bloodletter", "Infamous Bloodletter",
            "Shattersword", "Swiftblade", "Infamous Swiftblade", "Combatant", "Infamous Combatant",
            "Mysterious Diver", "Infamous Mysterious Diver"],
            [StatType.Str, StatType.Dex]);

        AddMercs(["Warpriest", "Infamous Warpriest", "Infamous Warpriest of the Ruckus",
            "Cardinal", "Infamous Cardinal", "Fallen Reverend", "Infamous Fallen Reverend",
            "Winter Deacon", "Storming Zealot", "Infamous Storming Zealot", "Flaming Charlatan"],
            [StatType.Str, StatType.Int]);

        AddMercs(["Blade Ambusher", "Infamous Blade Ambusher", "Shock Ambusher", "Infamous Shock Ambusher",
            "Frost Ambusher", "Bladereach", "Bladecaster", "Infamous Bladecaster",
            "Bladebitter", "Infamous Bladebitter"],
            [StatType.Dex, StatType.Int]);

        AddMercs(["Sanguimancer", "Infamous Sanguimancer", "Kineticist", "Infamous Kineticist"],
            [StatType.Str, StatType.Dex, StatType.Int]);

        return mercStats;
    }

    #region Core Plugin Methods
    public override bool Initialise()
    {
        _settingsDrawer = new MercScannerSettingsDrawer(Settings);

        _skillAttributeMap = new Dictionary<string, StatType?>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var gems = GameController.Files.SkillGems.EntriesList;
            foreach (var gem in gems)
            {
                var name = gem.ItemType?.BaseName;
                if (name == null) continue;

                var attr = gem.SocketType switch
                {
                    SkillGemDatSocketType.Red => (StatType?)StatType.Str,
                    SkillGemDatSocketType.Green => (StatType?)StatType.Dex,
                    SkillGemDatSocketType.Blue => (StatType?)StatType.Int,
                    _ => null
                };

                _skillAttributeMap.TryAdd(name, attr);
                var noSpace = name.Replace(" ", "");
                if (noSpace != name) _skillAttributeMap.TryAdd(noSpace, attr);
            }
        }
        catch (Exception ex) { LogError($"Failed to load skill gems: {ex.Message}"); }

        foreach (var mercName in MercenaryStats.Keys)
        {
            if (!Settings.MercenaryTiers.ContainsKey(mercName))
            {
                Settings.MercenaryTiers[mercName] = 0;
            }
        }
        return true;
    }

    public override Job Tick() => null;

    public override void Render()
    {
        var panels = GameController.IngameState.IngameUi;

        if ((!Settings.IgnoreLargePanels.Value && panels.LargePanels.Any(x => x.IsVisible)) ||
            (!Settings.IgnoreFullscreenPanels.Value && panels.FullscreenPanels.Any(x => x.IsVisible)))
        {
            return;
        }

        DrawMercenaryOverlays();

        if (!panels.MercenaryEncounterWindow.IsVisible && !panels.AllyEquipmentWindow.IsVisible)
        {
            DrawFrameOnMercItems();
        }
    }
    #endregion

    #region Settings UI
    public override void DrawSettings()
    {
        _settingsDrawer.Draw();
    }
    #endregion

    #region Drawing Logic
    private void DrawFrameOnMercItems()
    {
        if (!Settings.HighlightMercenary.Value) return;

        foreach (var label in GameController.IngameState.IngameUi.ItemsOnGroundLabels)
        {
            if (label.Label is not { IsVisible: true }) continue;

            var nameElement = label.Label.Children.ElementAtOrDefault(1)?.Children.ElementAtOrDefault(0);
            if (nameElement?.Text is not { } itemName) continue;

            var matchedMercKey = MercenaryKeysSorted.FirstOrDefault(mercName =>
                itemName.Contains(mercName, StringComparison.OrdinalIgnoreCase));

            if (matchedMercKey != null)
            {
                var iconElement = label.Label.Children.ElementAtOrDefault(0);
                if (iconElement == null) continue;

                var stats = MercenaryStats[matchedMercKey];
                var labelRect = label.Label.GetClientRect();
                int tierValue = Settings.MercenaryTiers.GetValueOrDefault(matchedMercKey, 0);
                var iconRect = iconElement.GetClientRect();

                var outerFrameRect = iconRect;
                outerFrameRect.Inflate(-5, -5);

                DrawTierFrame(labelRect, outerFrameRect, tierValue);

                DrawNestedFrames(iconRect, stats);

                DrawTierLabel(label.Label, matchedMercKey);
            }
        }
    }

    private void DrawTierFrame(RectangleF fillRect, RectangleF snakeRect, int tierValue)
    {
        if (tierValue == 0) return;
        var (_, tierColor) = GetTierInfo(tierValue);

        if (Settings.ShowTierFrame.Value)
        {
            var fillAlpha = Settings.TierFrameFillOpacity.Value / 100f;
            var fillColor = new Color(tierColor.R, tierColor.G, tierColor.B, (byte)(fillAlpha * 255));
            var paddedRect = fillRect;
            paddedRect.Inflate(4, 4);
            Graphics.DrawBox(paddedRect, fillColor);
        }

        if (Settings.ShowTierSnake.Value && tierValue == 1)
        {
            DrawSnakeEffect(snakeRect, tierColor,
                Settings.TierSnakeSpeed.Value,
                Settings.TierSnakeIntensity.Value,
                Graphics.DrawBox);
        }
    }

    private void DrawTierLabel(Element label, string mercName)
    {
        if (!Settings.ShowTierText.Value) return;

        int tierValue = Settings.MercenaryTiers.GetValueOrDefault(mercName, 0);
        if (tierValue == 0) return;

        (string tierText, Color tierColor) = GetTierInfo(tierValue);

        var labelRect = label.GetClientRect();
        var textSize = ImGui.CalcTextSize(tierText);

        float centerX = labelRect.X + labelRect.Width / 2;
        float newX = centerX - textSize.X / 2;
        float newY = labelRect.Bottom - 5;

        var drawPos = new System.Numerics.Vector2(newX, newY);

        Graphics.DrawTextWithBackground(tierText, drawPos, tierColor, Settings.BackgroundColor.Value);
    }

    private (string, Color) GetTierInfo(int tierValue) => tierValue switch
    {
        1 => ("S Tier", Settings.STierColor.Value),
        2 => ("A Tier", Settings.ATierColor.Value),
        3 => ("B Tier", Settings.BTierColor.Value),
        4 => ("C Tier", Settings.CTierColor.Value),
        _ => ("None", Color.White)
    };

    private void DrawNestedFrames(RectangleF initialBox, List<StatType> stats)
    {
        initialBox.Inflate(-5, -5);
        const int frameThickness = 2;
        const int frameSpacing = 3;

        for (var i = 0; i < stats.Count; i++)
        {
            var stat = stats[i];
            var color = GetColorForStat(stat);
            var box = initialBox;
            box.Inflate(-i * frameSpacing, -i * frameSpacing);
            Graphics.DrawFrame(box, color, frameThickness);
        }
    }

    private Color GetColorForStat(StatType stat) => stat switch
    {
        StatType.Str => Settings.StrColor.Value,
        StatType.Dex => Settings.DexColor.Value,
        StatType.Int => Settings.IntColor.Value,
        _ => Color.White
    };

    private void DrawMercenaryOverlays()
    {
        var lineHeight = ImGui.GetTextLineHeight();
        var barHeight = 8f;

        if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Monster, out var monsters)) return;
        foreach (var mon in monsters)
        {
            if (mon.DistancePlayer > Settings.MaxMercDistance.Value) continue;
            if (!mon.Metadata.StartsWith("Metadata/Monsters/Mercenaries/", StringComparison.Ordinal)) continue;
            if (mon.IsHostile) continue;

            var isHired = mon.Metadata.Contains("Allied", StringComparison.Ordinal);
            var merScreenPos = GameController.IngameState.Camera.WorldToScreen(mon.PosNum);
            if (merScreenPos.Y < 0) continue;

            var name = mon.RenderName;
            var stats = name != null ? MercenaryStats.GetValueOrDefault(name) : null;

            mon.TryGetComponent<Actor>(out var actor);
            mon.TryGetComponent<Life>(out var life);
            mon.TryGetComponent<Buffs>(out var buffs);

            var centerX = merScreenPos.X;

            if (isHired)
            {
                if (!Settings.ShowHiredMercOverlays.Value) continue;

                if (life != null)
                {
                    var barY = merScreenPos.Y;
                    if (Settings.ShowHiredMercHpBar.Value && life.MaxHP > 0)
                        barY = DrawSimpleBar(centerX, barY, life.CurHP, life.MaxHP, Settings.HpBarColor.Value, barHeight);
                    if (Settings.ShowHiredMercEsBar.Value && life.MaxES > 0)
                        barY = DrawSimpleBar(centerX, barY, life.CurES, life.MaxES, Settings.EsBarColor.Value, barHeight);
                }

                if (actor != null && Settings.ShowHiredMercActionPanel.Value)
                    DrawHiredMercActionPanel(actor, buffs, centerX, merScreenPos.Y + lineHeight, lineHeight);

                continue;
            }

            if (Settings.ShowEntityOverlays.Value)
            {
                if (stats != null && Settings.ShowEntityFrames.Value)
                    DrawEntityFrames(centerX, merScreenPos.Y, stats);

                if (name != null && Settings.ShowEntityTier.Value)
                {
                    int tierValue = Settings.MercenaryTiers.GetValueOrDefault(name, 0);
                    if (tierValue != 0)
                    {
                        var (tierText, tierColor) = GetTierInfo(tierValue);
                        DrawTextAbove(centerX, tierText, tierColor, merScreenPos.Y, lineHeight);
                    }
                }

                if (life != null)
                {
                    var barY = merScreenPos.Y;
                    if (Settings.ShowHpBar.Value && life.MaxHP > 0)
                        barY = DrawSimpleBar(centerX, barY, life.CurHP, life.MaxHP, Settings.HpBarColor.Value, barHeight);
                    if (Settings.ShowEsBar.Value && life.MaxES > 0)
                        barY = DrawSimpleBar(centerX, barY, life.CurES, life.MaxES, Settings.EsBarColor.Value, barHeight);
                }

                if (buffs != null)
                    DrawEntityBuffs(buffs, centerX, merScreenPos.Y + lineHeight);
            }

            if (actor != null)
            {
                var skillsStartY = merScreenPos.Y + lineHeight + 4;
                var skillsEndY = DrawEntitySkills(actor, centerX, skillsStartY, lineHeight);

                if (Settings.SeparateAuraDisplay.Value && Settings.AutoDetectAuras.Value)
                {
                    DrawEntityAuras(actor, buffs, centerX, skillsEndY, lineHeight);
                }
            }
        }
    }

    private float DrawTextAbove(float centerX, string text, Color color, float currentTop, float lineHeight)
    {
        var textSize = ImGui.CalcTextSize(text);
        var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, currentTop - textSize.Y - 2);
        Graphics.DrawTextWithBackground(text, drawPos, color, Settings.BackgroundColor.Value);
        return drawPos.Y;
    }

    private void DrawHiredMercActionPanel(Actor actor, Buffs buffs, float centerX, float startY, float lineHeight)
    {
        var line = 0;

        var usingSkill = (actor.Action & ActionFlags.UsingAbility) != 0;
        var isMoving = (actor.Action & ActionFlags.Moving) != 0 && !usingSkill;

        if (usingSkill && actor.CurrentAction?.Skill != null)
        {
            var skillName = GetSkillDisplayName(actor.CurrentAction.Skill);
            var text = $"> {skillName}";
            var textSize = ImGui.CalcTextSize(text);
            var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY);
            var textRect = new RectangleF(drawPos.X - 2, drawPos.Y - 1, textSize.X + 4, textSize.Y + 2);
            Graphics.DrawFrame(textRect, new Color(0.2f, 1.0f, 0.4f, 1f), 1);
            Graphics.DrawTextWithBackground(text, drawPos, new Color(0.2f, 1.0f, 0.4f, 1f), Settings.BackgroundColor.Value);
            line++;
        }
        else if (isMoving)
        {
            var text = "- Moving...";
            var textSize = ImGui.CalcTextSize(text);
            var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY);
            Graphics.DrawTextWithBackground(text, drawPos, new Color(0.6f, 0.6f, 0.6f, 1f), Settings.BackgroundColor.Value);
            line++;
        }

        if (actor.ActorSkills != null)
        {
            foreach (var skill in actor.ActorSkills)
            {
                if (skill.Name is null or "Move" or "EASMercenaryPortalOut") continue;
                if (skill.IsUsing || skill.IsChanneling) continue;
                if (!skill.IsOnCooldown) continue;

                var skillName = GetSkillDisplayName(skill);
                var cooldownInfo = skill.CooldownInfo?.SkillCooldowns is { Count: > 0 }
                    ? skill.CooldownInfo.SkillCooldowns[0] : null;

                var cdRemaining = cooldownInfo?.Remaining ?? skill.Cooldown;
                var cdTotal = skill.Cooldown;
                var cdText = cooldownInfo != null
                    ? $" [{cooldownInfo.Remaining:F1}s]"
                    : skill.Cooldown > 0 ? $" [{skill.Cooldown:F1}s]" : " [cd]";

                var text = $"{skillName}{cdText}";
                var textSize = ImGui.CalcTextSize(text);
                var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);
                Graphics.DrawTextWithBackground(text, drawPos, new Color(0.5f, 0.5f, 0.5f, 1f), Settings.BackgroundColor.Value);

                const float cdBarWidth = 100f;
                const float cdBarHeight = 4f;
                var barY = drawPos.Y + textSize.Y + 2;
                var barX = centerX - cdBarWidth / 2;
                var barRect = new RectangleF(barX, barY, cdBarWidth, cdBarHeight);
                Graphics.DrawBox(barRect, new Color(0, 0, 0, 140));

                if (cdTotal > 0)
                {
                    var fill = Math.Clamp(1f - (float)cdRemaining / cdTotal, 0f, 1f);
                    var fillRect = new RectangleF(barX, barY, cdBarWidth * fill, cdBarHeight);
                    Graphics.DrawBox(fillRect, new Color(0.3f, 0.7f, 1f, 0.8f));
                }

                line++;
            }

            foreach (var skill in actor.ActorSkills)
            {
                if (skill.Name is null or "Move" or "EASMercenaryPortalOut") continue;
                if (skill.IsUsing || skill.IsChanneling || skill.IsOnCooldown) continue;

                var auraName = GetSkillDisplayName(skill);
                var isAura = IsAuraFilterMatch(skill, auraName, skill.EffectsPerLevel);

                if (!isAura) continue;

                var activeBuff = FindActiveAuraBuff(buffs, skill, auraName);

                var isActive = activeBuff != null;
                var timerText = isActive && activeBuff.MaxTime > 0 && activeBuff.MaxTime < 86400f
                    ? $" {activeBuff.Timer:F1}s"
                    : isActive ? "" : "";

                var text = isActive ? $"+ {auraName}{timerText}" : $"- {auraName}";
                var textSize = ImGui.CalcTextSize(text);
                var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);

                if (isActive)
                {
                    var textRect = new RectangleF(drawPos.X - 2, drawPos.Y - 1, textSize.X + 4, textSize.Y + 2);
                    Graphics.DrawFrame(textRect, Settings.AuraActiveColor.Value, 1);
                }

                var color = isActive ? Settings.AuraActiveColor.Value : new Color(0.45f, 0.45f, 0.45f, 1f);
                Graphics.DrawTextWithBackground(text, drawPos, color, Settings.BackgroundColor.Value);
                line++;
            }
        }
    }

    private float DrawSimpleBar(float centerX, float startY, int cur, int max, Color fillColor, float barHeight)
    {
        if (max <= 0) return startY;

        const float barWidth = 120f;
        var barX = centerX - barWidth / 2;
        var bgRect = new RectangleF(barX, startY, barWidth, barHeight);
        var bgColor = new Color(0, 0, 0, 180);

        Graphics.DrawBox(bgRect, bgColor);

        var fillWidth = barWidth * Math.Min(1f, (float)cur / max);
        var fillRect = new RectangleF(barX, startY, fillWidth, barHeight);
        Graphics.DrawBox(fillRect, fillColor);

        return startY + barHeight + 1;
    }

    private void DrawEntityFrames(float centerX, float centerY, List<StatType> stats)
    {
        const float frameSize = 20f;
        var box = new RectangleF(centerX - frameSize, centerY - frameSize, frameSize * 2, frameSize * 2);
        DrawNestedFrames(box, stats);
    }

    private void DrawEntityBuffs(Buffs buffs, float centerX, float startY)
    {
        var line = 0;
        var lineHeight = ImGui.GetTextLineHeight();

        foreach (var buff in buffs.BuffsList)
        {
            var buffName = buff.DisplayName ?? buff.Name;
            if (string.IsNullOrWhiteSpace(buffName)) continue;

            var isHighlighted = Settings.SkillFilter.Content.Any(x =>
                !string.IsNullOrWhiteSpace(x.Value) &&
                (buffName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase)));

            if (!isHighlighted) continue;

            var timerText = buff.MaxTime > 0 && buff.MaxTime < 86400f ? $" {buff.Timer:F1}s" : "";
            var displayText = $"> {buffName}{timerText} <";
            var textSize = ImGui.CalcTextSize(displayText);
            var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);
            var textRect = new RectangleF(drawPos.X - 2, drawPos.Y - 1, textSize.X + 4, textSize.Y + 2);

            Graphics.DrawFrame(textRect, Settings.AuraActiveColor.Value, 1);
            Graphics.DrawTextWithBackground(displayText, drawPos,
                Settings.AuraActiveColor.Value, Settings.BackgroundColor.Value);
            line++;
        }
    }

    private float DrawEntitySkills(Actor actor, float centerX, float startY, float lineHeight)
    {
        var line = 0;

        if (actor.ActorSkills == null) return startY;
        foreach (var skill in actor.ActorSkills.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            if (skill.Name is "Move" or "EASMercenaryPortalOut") continue;

            var skillName = GetSkillDisplayName(skill);

            var isSkillHighlighted = !string.IsNullOrWhiteSpace(skillName) &&
                Settings.SkillFilter.Content.Any(x =>
                    !string.IsNullOrWhiteSpace(x.Value) &&
                    skillName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase));

            var isAura = IsAuraFilterMatch(skill, skillName, skill.EffectsPerLevel);

            if (!isSkillHighlighted && !isAura && !Settings.ShowAllSkills.Value) continue;

            if (isAura && Settings.SeparateAuraDisplay.Value) continue;

            var labeledName = isAura && !Settings.SeparateAuraDisplay.Value ? $">> {skillName} <<" : skillName;
            var showHighlight = isSkillHighlighted || (isAura && !Settings.SeparateAuraDisplay.Value);
            if (showHighlight) labeledName = $">> {skillName} <<";

            var skillColor = showHighlight
                ? Settings.HighlightSkillColor.Value
                : GetSkillColorForSkill(skill, skillName);

            var textSize = ImGui.CalcTextSize(labeledName);
            var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);

            var attrColor = GetSkillColorForSkill(skill, skillName);

            if (showHighlight)
            {
                var textRect = new RectangleF(drawPos.X - 3, drawPos.Y - 2, textSize.X + 6, textSize.Y + 4);
                var fillColor = new Color(attrColor.R, attrColor.G, attrColor.B, (byte)35);
                Graphics.DrawBox(textRect, fillColor);
                Graphics.DrawFrame(textRect, skillColor, 1);
            }

            Graphics.DrawTextWithBackground(labeledName, drawPos,
                skillColor, showHighlight ? new Color(attrColor.R, attrColor.G, attrColor.B, (byte)155) : Settings.BackgroundColor.Value);
            line++;
        }

        return startY + lineHeight * line;
    }

    private void DrawEntityAuras(Actor actor, Buffs buffs, float centerX, float startY, float lineHeight)
    {
        if (actor.ActorSkills == null) return;
        var line = 0;

        foreach (var skill in actor.ActorSkills.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            if (skill.Name is "Move" or "EASMercenaryPortalOut") continue;

            var auraName = GetSkillDisplayName(skill);

            if (!IsAuraFilterMatch(skill, auraName, skill.EffectsPerLevel)) continue;

            var activeBuff = FindActiveAuraBuff(buffs, skill, auraName);

            var isActive = activeBuff != null;
            var timerText = Settings.ShowAuraTimers.Value && isActive && activeBuff.MaxTime > 0 && activeBuff.MaxTime < 86400f
                ? $" {activeBuff.Timer:F1}s"
                : "";

            var displayText = isActive
                ? $"+ {auraName}{timerText}"
                : $"- {auraName}";

            var textSize = ImGui.CalcTextSize(displayText);

            var pillPaddingX = 6f;
            var pillPaddingY = 2f;
            var pillRect = new RectangleF(
                centerX - textSize.X / 2 - pillPaddingX,
                startY + lineHeight * line - pillPaddingY,
                textSize.X + pillPaddingX * 2,
                textSize.Y + pillPaddingY * 2);

            var isSkillHighlighted = !string.IsNullOrWhiteSpace(auraName) &&
                Settings.SkillFilter.Content.Any(x =>
                    !string.IsNullOrWhiteSpace(x.Value) &&
                    auraName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase));

            Color textColor;
            if (isActive)
            {
                Graphics.DrawBox(pillRect, new Color(Settings.AuraActiveColor.Value.R, Settings.AuraActiveColor.Value.G, Settings.AuraActiveColor.Value.B, (byte)40));
                Graphics.DrawFrame(pillRect, Settings.AuraActiveColor.Value, 1);
                textColor = Settings.AuraActiveColor.Value;
            }
            else
            {
                var color = isSkillHighlighted ? Settings.HighlightSkillColor.Value : Settings.AuraInactiveColor.Value;
                Graphics.DrawBox(pillRect, new Color(color.R, color.G, color.B, (byte)20));
                Graphics.DrawFrame(pillRect, color, 1);
                textColor = color;
            }

            var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);
            Graphics.DrawTextWithBackground(displayText, drawPos, textColor, Settings.BackgroundColor.Value);

            line++;
        }
    }

    private bool IsAuraFilterMatch(ActorSkill skill, string displayName, GrantedEffectsPerLevel effects)
    {
        var auraFilter = Settings.SkillFilter.Content.Any(x =>
            !string.IsNullOrWhiteSpace(x.Value) &&
            displayName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase));

        if (auraFilter) return true;

        if (!Settings.AutoDetectAuras.Value) return false;

        if (effects?.SkillGemWrapper?.ActiveSkill?.InternalName is { } internalName)
        {
            if (KnownAuraInternalNames.Contains(internalName)) return true;
        }

        return false;
    }

    private Color GetSkillColorForSkill(ActorSkill skill, string skillName)
    {
        if (_skillAttributeMap.TryGetValue(skillName, out var attr))
        {
            return attr switch
            {
                StatType.Str => Settings.StrColor.Value,
                StatType.Dex => Settings.DexColor.Value,
                StatType.Int => Settings.IntColor.Value,
                _ => Settings.DefaultSkillColor.Value
            };
        }

        if (skill.IsCry) return Settings.StrColor.Value;
        if (skill.IsMine || skill.IsTrap) return Settings.DexColor.Value;

        return Settings.MonsterSkillColor.Value;
    }

    private static void DrawSnakeEffect(RectangleF rect, Color baseColor, float animationSpeed, float animationIntensity, Action<RectangleF, Color> drawBoxAction)
    {
        var padding = 2 * animationIntensity;
        var lineThickness = 4 * animationIntensity;
        const int snakeLength = 60;

        var currentTime = DateTime.UtcNow.TimeOfDay.TotalSeconds;
        var snakePosition = currentTime * 100 * animationSpeed;

        var pathWidth = rect.Width + padding * 2;
        var pathHeight = rect.Height + padding * 2;
        var perimeter = (pathWidth + pathHeight) * 2;
        var startX = rect.X - padding;
        var startY = rect.Y - padding;

        for (int i = 0; i < snakeLength; i++)
        {
            var segmentOffset = (snakePosition - i) % perimeter;
            if (segmentOffset < 0) segmentOffset += perimeter;

            var fade = 1f - (i / (float)snakeLength);
            var alpha = (byte)Math.Max(20, fade * 200);

            var brightness = 0.5f + (fade * 0.5f);
            var r = (byte)Math.Min(255, baseColor.R * brightness);
            var g = (byte)Math.Min(255, baseColor.G * brightness);
            var b = (byte)Math.Min(255, baseColor.B * brightness);
            var segmentColor = new Color(r, g, b, alpha);

            float sx, sy;
            if (segmentOffset < pathWidth)
            {
                sx = startX + (float)segmentOffset;
                sy = startY;
            }
            else if (segmentOffset < pathWidth + pathHeight)
            {
                sx = startX + pathWidth;
                sy = startY + (float)(segmentOffset - pathWidth);
            }
            else if (segmentOffset < pathWidth * 2 + pathHeight)
            {
                sx = startX + pathWidth - (float)(segmentOffset - (pathWidth + pathHeight));
                sy = startY + pathHeight;
            }
            else
            {
                sx = startX;
                sy = startY + pathHeight - (float)(segmentOffset - (pathWidth * 2 + pathHeight));
            }

            drawBoxAction(
                new RectangleF(sx - lineThickness / 2, sy - lineThickness / 2, lineThickness, lineThickness),
                segmentColor
            );
        }
    }
    #endregion
}
