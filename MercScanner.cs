using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;

namespace MercScanner;

public class MercScanner : BaseSettingsPlugin<MercScannerSettings>
{
    private MercScannerSettingsDrawer _settingsDrawer;

    #region Data
    private enum StatType { Str, Dex, Int }
    private static readonly Dictionary<string, List<StatType>> MercenaryStats = InitializeMercStats();
    
    private static Dictionary<string, List<StatType>> InitializeMercStats()
    {
        var mercStats = new Dictionary<string, List<StatType>>();
        void AddMercs(IEnumerable<string> names, List<StatType> stats) { foreach (var name in names) mercStats[name] = stats; }
        AddMercs(new[] { "Eruptor", "Ripper", "Earthshaker", "Smoulderstrike", "Striker" }, new List<StatType> { StatType.Str });
        AddMercs(new[] { "Toxicologist", "Sniper", "Thunderquiver", "Flamequiver", "Manyshot" }, new List<StatType> { StatType.Dex });
        AddMercs(new[] { "Stormhand", "Frosthand", "Flamehand", "Withertouch", "Reanimator", "Cruel Mistress" }, new List<StatType> { StatType.Int });
        AddMercs(new[] { "Bastion", "Bloodletter", "Shattersword", "Swiftblade", "Combatant" }, new List<StatType> { StatType.Str, StatType.Dex });
        AddMercs(new[] { "Warpriest", "Fallen Reverend", "Winter Deacon", "Storming Zealot", "Flaming Charlatan" }, new List<StatType> { StatType.Str, StatType.Int });
        AddMercs(new[] { "Blade Ambusher", "Shock Ambusher", "Frost Ambusher", "Bladereach", "Bladecaster", "Bladebitter" }, new List<StatType> { StatType.Dex, StatType.Int });
        AddMercs(new[] { "Sanguimancer", "Kineticist" }, new List<StatType> { StatType.Str, StatType.Dex, StatType.Int });
        return mercStats;
    }
    #endregion

    #region Core Plugin Methods
    public override bool Initialise()
    {
        _settingsDrawer = new MercScannerSettingsDrawer(Settings);
        
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
            (!Settings.IgnoreFullscreenPanels.Value && panels.FullscreenPanels.Any(x => x.IsVisible)) ||
            panels.MercenaryEncounterWindow.IsVisible || panels.LeagueMechanicButtons.IsVisible)
        {
            return;
        }

        DrawSkillsOnIdleMercs();
        DrawFrameOnMercItems();
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
        if (!Settings.HighlightMercenaryItems.Value) return;

        foreach (var label in GameController.IngameState.IngameUi.ItemsOnGroundLabels)
        {
            if (label.Label is not { IsVisible: true }) continue;
            
            var nameElement = label.Label.Children.ElementAtOrDefault(1)?.Children.ElementAtOrDefault(0);
            if (nameElement?.Text is not { } itemName) continue;

            var matchedMercKey = MercenaryStats.Keys.FirstOrDefault(mercName => 
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

    private void DrawSkillsOnIdleMercs()
    {
        var idleMercs = GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
            .Where(x => x.Metadata.StartsWith("Metadata/Monsters/Mercenaries/", StringComparison.Ordinal) &&
                        !x.IsHostile &&
                        x.TryGetComponent<Positioned>(out var p) && p.Reaction == 70);

        foreach (var idleMerc in idleMercs)
        {
            if (!idleMerc.TryGetComponent<Actor>(out var actor)) continue;
            
            var line = 0;
            var merScreenPos = GameController.IngameState.Camera.WorldToScreen(idleMerc.PosNum);
            var lineHeight = ImGui.GetTextLineHeight();

            foreach (var skill in actor.ActorSkills.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
            {
                var fullSkillName = skill.Name;
                if (fullSkillName is "Move" or "EASMercenaryPortalOut") continue;

                var skillName = fullSkillName.EndsWith("Mercenary") ? fullSkillName[..^"Mercenary".Length] : fullSkillName;
                var isHighlighted = Settings.Auras.Content.Any(x => 
                    !string.IsNullOrWhiteSpace(x.Value) &&
                    (skillName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase) ||
                     fullSkillName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase)));

                if (!isHighlighted && !Settings.ShowAllSkills.Value) continue;

                var textSize = ImGui.CalcTextSize(skillName);
                var drawPos = new System.Numerics.Vector2(merScreenPos.X - textSize.X / 2, merScreenPos.Y + lineHeight * line);
                
                Graphics.DrawTextWithBackground(skillName, drawPos,
                    isHighlighted ? Settings.HighlightSkillColor.Value : Settings.DefaultSkillColor.Value, 
                    Settings.BackgroundColor.Value);
                line++;
            }
        }
    }
    #endregion
}