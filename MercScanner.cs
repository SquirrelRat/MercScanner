using System;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ImGuiNET;
using SharpDX;
using Color = System.Drawing.Color;
using Vector2 = System.Numerics.Vector2;

namespace MercScanner;

public class MercScanner : BaseSettingsPlugin<MercScannerSettings>
{
    public override bool Initialise()
    {
        return true;
    }

    public override Job Tick()
    {
        return null;
    }

    public override void Render()
    {
        if (!Settings.IgnoreLargePanels && GameController.IngameState.IngameUi.LargePanels.Any(x => x.IsVisible) ||
            !Settings.IgnoreFullscreenPanels && GameController.IngameState.IngameUi.FullscreenPanels.Any(x => x.IsVisible))
        {
            return;
        }

        foreach (var idleMerc in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster]
                     .Where(x => x.Metadata.StartsWith("Metadata/Monsters/Mercenaries/", StringComparison.Ordinal) &&
                                 !x.IsHostile &&
                                 x.TryGetComponent<Positioned>(out var positioned) &&
                                 positioned.Reaction == 70))
        {
            if (idleMerc.TryGetComponent<Actor>(out var actor))
            {
                var line = 0;
                var merScreenPos = GameController.IngameState.Camera.WorldToScreen(idleMerc.PosNum);
                var lineHeight = ImGui.GetTextLineHeight();
                foreach (var skill in actor.ActorSkills.Where(x => !string.IsNullOrWhiteSpace(x.Name)))
                {
                    var fullSkillName = skill.Name;
                    if (fullSkillName == "Move" || fullSkillName == "EASMercenaryPortalOut")
                    {
                        continue;
                    }

                    var skillName = fullSkillName;
                    if (skillName.EndsWith("Mercenary"))
                    {
                        skillName = skillName[..^"Mercenary".Length];
                    }

                    var isHighlighted = Settings.Auras.Content.Any(x => !string.IsNullOrWhiteSpace(x.Value) &&
                                                                        (skillName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase) ||
                                                                         fullSkillName.Contains(x.Value, StringComparison.InvariantCultureIgnoreCase)));
                    if (!isHighlighted && !Settings.ShowAllSkills)
                    {
                        continue;
                    }

                    Graphics.DrawTextWithBackground(skillName,
                        merScreenPos + new Vector2(0, lineHeight * line),
                        isHighlighted
                            ? Settings.HighlightSkillColor
                            : Settings.DefaultSkillColor, Settings.BackgroundColor);
                    line++;
                }
            }
        }
    }
}