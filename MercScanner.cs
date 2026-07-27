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
    #endregion

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
                DrawNestedFrames(iconElement.GetClientRect(), stats);
                
                DrawTierLabel(label.Label, matchedMercKey);
            }
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
            var topY = merScreenPos.Y;

            if (isHired)
            {
                if (!Settings.ShowHiredMercOverlays.Value) continue;

                if (life != null)
                {
                    if (Settings.ShowHiredMercEsBar.Value && life.MaxES > 0)
                        DrawVitalBar(centerX, topY, life.CurES, life.MaxES, Settings.EsBarColor.Value, barHeight, false);

                    if (Settings.ShowHiredMercHpBar.Value)
                        DrawVitalBar(centerX, topY, life.CurHP, life.MaxHP, Settings.HpBarColor.Value, barHeight, true);
                }

                if (actor != null && Settings.ShowHiredMercActionPanel.Value)
                    DrawHiredMercActionPanel(actor, buffs, centerX, merScreenPos.Y + lineHeight, lineHeight);

                continue;
            }

            var hasEsBar = false;

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
                        topY = DrawTextAbove(centerX, tierText, tierColor, topY, lineHeight);
                    }
                }

                if (life != null)
                {
                    if (Settings.ShowEsBar.Value && life.MaxES > 0)
                    {
                        topY = DrawVitalBar(centerX, topY, life.CurES, life.MaxES, Settings.EsBarColor.Value, barHeight, false);
                        hasEsBar = true;
                    }

                    if (Settings.ShowHpBar.Value)
                        topY = DrawVitalBar(centerX, topY, life.CurHP, life.MaxHP, Settings.HpBarColor.Value, barHeight, true);
                }

                if (buffs != null)
                    DrawEntityBuffs(buffs, centerX, merScreenPos.Y + lineHeight);
            }

            if (actor != null)
                DrawEntitySkills(actor, centerX, merScreenPos.Y + lineHeight + (hasEsBar ? 4 : 0), lineHeight);
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
            var effects = actor.CurrentAction.Skill.EffectsPerLevel;
            var displayName = effects?.SkillGemWrapper?.ActiveSkill?.DisplayName
                              ?? effects?.SkillGemWrapper?.Name
                              ?? actor.CurrentAction.Skill.Name;
            var skillName = displayName.EndsWith("Mercenary") ? displayName[..^"Mercenary".Length] : displayName;
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

                var effects = skill.EffectsPerLevel;
                var displayName = effects?.SkillGemWrapper?.ActiveSkill?.DisplayName
                                  ?? effects?.SkillGemWrapper?.Name;
                var skillName = !string.IsNullOrWhiteSpace(displayName)
                    ? (displayName.EndsWith("Mercenary") ? displayName[..^"Mercenary".Length] : displayName)
                    : (skill.Name.EndsWith("Mercenary") ? skill.Name[..^"Mercenary".Length] : skill.Name);

                var cdText = skill.CooldownInfo?.SkillCooldowns is { Count: > 0 }
                    ? $" [{skill.CooldownInfo.SkillCooldowns[0].Remaining:F1}s]"
                    : skill.Cooldown > 0 ? $" [{skill.Cooldown:F1}s]" : " [cd]";

                var text = $"{skillName}{cdText}";
                var textSize = ImGui.CalcTextSize(text);
                var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);
                Graphics.DrawTextWithBackground(text, drawPos, new Color(0.5f, 0.5f, 0.5f, 1f), Settings.BackgroundColor.Value);
                line++;
            }

            foreach (var skill in actor.ActorSkills)
            {
                if (skill.Name is null or "Move" or "EASMercenaryPortalOut") continue;
                if (skill.IsUsing || skill.IsChanneling || skill.IsOnCooldown) continue;

                var effects = skill.EffectsPerLevel;
                var displayName = effects?.SkillGemWrapper?.ActiveSkill?.DisplayName
                                  ?? effects?.SkillGemWrapper?.Name;

                var auraName = !string.IsNullOrWhiteSpace(displayName)
                    ? (displayName.EndsWith("Mercenary") ? displayName[..^"Mercenary".Length] : displayName)
                    : (skill.Name.EndsWith("Mercenary") ? skill.Name[..^"Mercenary".Length] : skill.Name);

                var isAura = Settings.Auras.Content.Any(x =>
                    !string.IsNullOrWhiteSpace(x.Value) &&
                    auraName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase));

                if (!isAura) continue;

                Buff activeBuff = null;
                if (buffs?.BuffsList != null)
                {
                    activeBuff = buffs.BuffsList.FirstOrDefault(b =>
                    {
                        var bn = b.DisplayName ?? b.Name;
                        return !string.IsNullOrWhiteSpace(bn) &&
                               bn.Contains(auraName, StringComparison.InvariantCultureIgnoreCase);
                    });
                }

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
                    Graphics.DrawFrame(textRect, Settings.HighlightSkillColor.Value, 1);
                }

                var color = isActive ? Settings.HighlightSkillColor.Value : new Color(0.45f, 0.45f, 0.45f, 1f);
                Graphics.DrawTextWithBackground(text, drawPos, color, Settings.BackgroundColor.Value);
                line++;
            }
        }
    }

    private float DrawVitalBar(float centerX, float topY, int cur, int max, Color fillColor, float barHeight, bool textAbove)
    {
        const float barWidth = 120f;
        var barX = centerX - barWidth / 2;
        var y = Math.Max(0, topY - barHeight);
        var bgRect = new RectangleF(barX, y, barWidth, barHeight);
        var bgColor = new Color(0, 0, 0, 180);

        Graphics.DrawBox(bgRect, bgColor);
        if (max > 0)
        {
            var fillWidth = barWidth * Math.Min(1f, (float)cur / max);
            var fillRect = new RectangleF(barX, y, fillWidth, barHeight);
            Graphics.DrawBox(fillRect, fillColor);
        }

        var text = $"{cur}/{max}";
        var textSize = ImGui.CalcTextSize(text);
        var textY = textAbove ? y - textSize.Y - 1 : y + barHeight + 1;
        var textPos = new System.Numerics.Vector2(centerX - textSize.X / 2, textY);
        Graphics.DrawTextWithBackground(text, textPos, Color.White, Settings.BackgroundColor.Value);

        return y - 2;
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

            var isHighlighted = Settings.Auras.Content.Any(x =>
                !string.IsNullOrWhiteSpace(x.Value) &&
                (buffName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase)));

            if (!isHighlighted) continue;

            var timerText = buff.MaxTime > 0 && buff.MaxTime < 86400f ? $" {buff.Timer:F1}s" : "";
            var displayText = $"> {buffName}{timerText} <";
            var textSize = ImGui.CalcTextSize(displayText);
            var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);
            var textRect = new RectangleF(drawPos.X - 2, drawPos.Y - 1, textSize.X + 4, textSize.Y + 2);

            Graphics.DrawFrame(textRect, Settings.HighlightSkillColor.Value, 1);
            Graphics.DrawTextWithBackground(displayText, drawPos,
                Settings.HighlightSkillColor.Value, Settings.BackgroundColor.Value);
            line++;
        }
    }

    private void DrawEntitySkills(Actor actor, float centerX, float startY, float lineHeight)
    {
        var line = 0;

        if (actor.ActorSkills == null) return;
        foreach (var skill in actor.ActorSkills.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
        {
            if (skill.Name is "Move" or "EASMercenaryPortalOut") continue;

            var effects = skill.EffectsPerLevel;
            var displayName = effects?.SkillGemWrapper?.ActiveSkill?.DisplayName
                              ?? effects?.SkillGemWrapper?.Name;
            var skillName = !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : skill.Name.EndsWith("Mercenary") ? skill.Name[..^"Mercenary".Length] : skill.Name;

            var isHighlighted = Settings.Auras.Content.Any(x =>
                !string.IsNullOrWhiteSpace(x.Value) &&
                skillName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase));

            if (!isHighlighted && !Settings.ShowAllSkills.Value) continue;

            var labeledName = isHighlighted ? $"> {skillName} <" : skillName;
            var skillColor = isHighlighted
                ? Settings.HighlightSkillColor.Value
                : GetSkillColorForSkill(skill, skillName);

            var textSize = ImGui.CalcTextSize(labeledName);
            var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);

            if (isHighlighted)
            {
                var textRect = new RectangleF(drawPos.X - 2, drawPos.Y - 1, textSize.X + 4, textSize.Y + 2);
                Graphics.DrawFrame(textRect, Settings.HighlightSkillColor.Value, 1);
            }

            Graphics.DrawTextWithBackground(labeledName, drawPos,
                skillColor, Settings.BackgroundColor.Value);
            line++;
        }
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

    #endregion
}