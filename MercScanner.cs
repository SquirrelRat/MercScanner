using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.FilesInMemory;
using ExileCore.Shared.Enums;
using ImGuiNET;
using SharpDX;

namespace MercScanner;

public partial class MercScanner : BaseSettingsPlugin<MercScannerSettings>
{
    private Dictionary<string, StatType?> _skillAttributeMap;
    private FlameLinkController _flameLink;
    private MercPanelScanner _panelScanner;
    private int _selectedAuraIndex = -1;
    private int _badSelectedAuraIndex = -1;
    private string _skillFilterInput = "";
    private string _badSkillFilterInput = "";

    #region Data
    internal enum StatType { Str, Dex, Int }
    internal enum MercTier { None = 0, S = 1, A = 2, B = 3, C = 4 }
    internal enum CooldownBarStyle { Inline = 0, BelowText = 1, FillBackground = 2 }

    internal static readonly string[] CooldownBarStyleLabels =
        ["Inline", "Below Text", "Fill Background"];
    internal static readonly Dictionary<string, List<StatType>> MercenaryStats = InitializeMercStats();
    internal static readonly string[] MercenaryKeysSorted = [.. MercenaryStats.Keys.OrderByDescending(k => k.Length)];

    internal static readonly (string InternalName, string DisplayName)[] KnownAuraEntries =
    [
        ("anger", "Anger"),
        ("wrath", "Wrath"),
        ("hatred", "Hatred"),
        ("determination", "Determination"),
        ("grace", "Grace"),
        ("discipline", "Discipline"),
        ("haste", "Haste"),
        ("malevolence", "Malevolence"),
        ("pride", "Pride"),
        ("zealotry", "Zealotry"),
        ("clarity", "Clarity"),
        ("vitality", "Vitality"),
        ("precision", "Precision"),
        ("envy", "Envy"),
        ("purity_of_fire", "Purity of Fire"),
        ("purity_of_ice", "Purity of Ice"),
        ("purity_of_lightning", "Purity of Lightning"),
        ("herald_of_ash", "Herald of Ash"),
        ("herald_of_ice", "Herald of Ice"),
        ("herald_of_thunder", "Herald of Thunder"),
        ("herald_of_agony", "Herald of Agony"),
        ("herald_of_purity", "Herald of Purity"),
        ("aspect_of_the_spider", "Aspect of the Spider"),
        ("aspect_of_the_avian", "Aspect of the Avian"),
        ("aspect_of_the_crab", "Aspect of the Crab"),
        ("aspect_of_the_cat", "Aspect of the Cat"),
        ("war_banner", "War Banner"),
        ("dread_banner", "Dread Banner"),
        ("defiance_banner", "Defiance Banner"),
        ("arctic_armour", "Arctic Armour"),
        ("summon_skitterbots", "Summon Skitterbots"),
        ("vaal_vitality", "Vaal Vitality"),
    ];

    internal static readonly HashSet<string> KnownAuraInternalNames = [.. KnownAuraEntries.Select(e => e.InternalName)];

    internal const float PermanentBuffTime = 86400f;

    #endregion

    #region Helpers
    private static bool IsDisplayableSkill(ActorSkill skill) =>
        skill.Name is not null and not "" and not "Move" and not "EASMercenaryPortalOut";

    private static bool IsMercenary(Entity m) =>
        !m.IsHostile && m.Metadata.StartsWith("Metadata/Monsters/Mercenaries/", StringComparison.Ordinal);

    private static bool IsHiredMerc(Entity m) =>
        m.Metadata.Contains("Allied", StringComparison.Ordinal);

    private static string StripMercenarySuffix(string s) =>
        s.EndsWith("Mercenary") ? s[..^"Mercenary".Length] : s;

    private bool MatchesFilter(string text) =>
        MatchesAny(Settings.SkillFilter, text);

    private bool MatchesBadFilter(string text) =>
        MatchesAny(Settings.BadSkillFilter, text);

    internal static readonly Dictionary<string, string> MercenarySkillAliases = new()
    {
        ["Icicle Rain"] = "Blast Rain",
    };

    private static string ExpandAlias(string text)
    {
        if (MercenarySkillAliases.TryGetValue(text, out var alias)) return alias;
        foreach (var (key, value) in MercenarySkillAliases)
        {
            if (value.Equals(text, StringComparison.InvariantCultureIgnoreCase)) return key;
        }
        return text;
    }

    private static bool MatchesAny(List<string> list, string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var alias = ExpandAlias(text);
        return list.Any(x =>
            !string.IsNullOrWhiteSpace(x) &&
            (text.Contains(x, StringComparison.InvariantCultureIgnoreCase) ||
             alias.Contains(x, StringComparison.InvariantCultureIgnoreCase)));
    }

    private static string GetSkillDisplayName(ActorSkill skill)
    {
        var effects = skill.EffectsPerLevel;
        var active = effects?.SkillGemWrapper?.ActiveSkill;

        var displayName = active?.DisplayName ?? effects?.SkillGemWrapper?.Name;
        if (IsUsableDisplayName(displayName))
            return StripMercenarySuffix(displayName);

        var internalName = active?.InternalName;
        if (!string.IsNullOrWhiteSpace(internalName))
            return HumanizeInternalName(internalName);

        return StripMercenarySuffix(string.IsNullOrWhiteSpace(displayName) ? skill.Name : displayName);
    }

    private static bool IsUsableDisplayName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        !name.Contains("[DNT]", StringComparison.OrdinalIgnoreCase) &&
        !name.StartsWith("[", StringComparison.Ordinal);

    private static string HumanizeInternalName(string internalName)
    {
        var words = internalName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var builder = new System.Text.StringBuilder();
        foreach (var word in words)
        {
            if (builder.Length > 0) builder.Append(' ');
            builder.Append(char.ToUpperInvariant(word[0]) + word[1..]);
        }
        return builder.ToString();
    }

    private static int GetStatInt(Stats stats, GameStat stat)
    {
        return stats.StatDictionary != null && stats.StatDictionary.TryGetValue(stat, out var value) ? value : 0;
    }

    private static List<StatType> GetMercStatTypes(Entity mon, string name)
    {
        if (mon.TryGetComponent<Stats>(out var stats) && GetStatInt(stats, GameStat.Level) > 0)
        {
            var str = GetStatInt(stats, GameStat.Strength);
            var dex = GetStatInt(stats, GameStat.Dexterity);
            var intel = GetStatInt(stats, GameStat.Intelligence);
            if (str + dex + intel > 0)
            {
                var threshold = Math.Max(1, (int)(Math.Max(str, Math.Max(dex, intel)) * 0.72f));
                var list = new List<StatType>(3);
                if (str >= threshold) list.Add(StatType.Str);
                if (dex >= threshold) list.Add(StatType.Dex);
                if (intel >= threshold) list.Add(StatType.Int);
                return list;
            }
        }

        return name != null ? MercenaryStats.GetValueOrDefault(name) : null;
    }

    private static int? GetMercLevel(Entity mon)
    {
        if (mon.TryGetComponent<Stats>(out var stats))
        {
            var level = GetStatInt(stats, GameStat.Level);
            if (level > 0) return level;
        }
        return null;
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
        return bySourceSkill;
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
        _flameLink = new FlameLinkController(this);
        _panelScanner = new MercPanelScanner(this);

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

    public override Job Tick()
    {
        _flameLink?.Update(GetHiredMercs());
        return null;
    }

    private List<Entity> GetHiredMercs()
    {
        if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Monster, out var monsters)) return new List<Entity>();
        return monsters.Where(IsMercenary).Where(IsHiredMerc).ToList();
    }

    public override void Render()
    {
        var panels = GameController.IngameState.IngameUi;
        var encounter = panels.MercenaryEncounterWindow;
        var ally = panels.AllyEquipmentWindow;

        if ((!Settings.IgnoreLargePanels.Value && panels.LargePanels.Any(x => x.IsVisible && x.Address != encounter.Address && x.Address != ally.Address)) ||
            (!Settings.IgnoreFullscreenPanels.Value && panels.FullscreenPanels.Any(x => x.IsVisible && x.Address != encounter.Address && x.Address != ally.Address)))
        {
            return;
        }

        if (encounter.IsVisible)
        {
            DrawEncounterPanelBorders();
            return;
        }

        DrawMercenaryOverlays();
        DrawOffScreenIndicators();

        if (!ally.IsVisible)
        {
            DrawFrameOnMercItems();
        }
    }
    #endregion

    #region Drawing Logic
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

    private static readonly string[] KnownAuraDisplayNames =
        KnownAuraEntries.Select(e => e.DisplayName).ToArray();

    private void AddAuraToFilter(string displayName) =>
        AddFilterEntry(Settings.SkillFilter, displayName);

    private void AddBadAuraToFilter(string displayName) =>
        AddFilterEntry(Settings.BadSkillFilter, displayName);

    private void AddSkillFilterEntry(string text) =>
        AddFilterEntry(Settings.SkillFilter, text);

    private void AddBadSkillFilterEntry(string text) =>
        AddFilterEntry(Settings.BadSkillFilter, text);

    private static void AddFilterEntry(List<string> list, string text)
    {
        var entry = text.Trim();
        if (entry.Length == 0) return;
        if (list.Any(x =>
                x.Equals(entry, StringComparison.InvariantCultureIgnoreCase))) return;
        list.Add(entry);
    }

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
                var tierValue = (MercTier)Settings.MercenaryTiers.GetValueOrDefault(matchedMercKey, 0);
                var iconRect = iconElement.GetClientRect();

                var outerFrameRect = iconRect;
                outerFrameRect.Inflate(-5, -5);

                DrawTierFrame(labelRect, outerFrameRect, tierValue);

                DrawNestedFrames(iconRect, stats);

                DrawTierLabel(label.Label, matchedMercKey);
            }
        }
    }

    private void DrawEncounterPanelBorders()
    {
        if (!Settings.HighlightEncounterPanelBorders.Value) return;

        var rows = _panelScanner.Read();
        foreach (var row in rows)
        {
            var skillColor = BorderColorFor(row.Name);
            if (skillColor != null)
                Graphics.DrawFrame(row.Rect, skillColor.Value, 2);

            var isBadSkill = MatchesBadFilter(row.Name);

            foreach (var support in row.Supports)
            {
                if (isBadSkill) continue;

                var tier = ResolveSupportRating(row.Name, support.Name);
                if (tier == null) continue;

                var tierColor = TierColor(tier.Value);
                Graphics.DrawFrame(support.Rect, tierColor, 2);
                DrawSupportTierLetter(support.Rect, tier.Value, tierColor);
            }
        }
    }

    private void DrawSupportTierLetter(RectangleF gemRect, SupportTier tier, Color tierColor)
    {
        var letter = TierLetter(tier);
        var letterSize = ImGui.CalcTextSize(letter);
        var drawPos = new System.Numerics.Vector2(
            gemRect.X + gemRect.Width - letterSize.X - 2,
            gemRect.Y + 1);
        Graphics.DrawTextWithBackground(letter, drawPos, tierColor, Settings.BackgroundColor.Value);
    }

    private Color? BorderColorFor(string name)
    {
        if (MatchesFilter(name)) return Settings.HighlightSkillColor.Value;
        if (MatchesBadFilter(name)) return Settings.BadSkillColor.Value;
        return null;
    }

    private void DrawTierFrame(RectangleF fillRect, RectangleF snakeRect, MercTier tierValue)
    {
        if (tierValue == MercTier.None) return;
        var (_, tierColor) = GetTierInfo(tierValue);

        if (Settings.ShowTierFrame.Value)
        {
            var fillAlpha = Settings.TierFrameFillOpacity.Value / 100f;
            var fillColor = new Color(tierColor.R, tierColor.G, tierColor.B, (byte)(fillAlpha * 255));
            var paddedRect = fillRect;
            paddedRect.Inflate(4, 4);
            Graphics.DrawBox(paddedRect, fillColor);
        }

        if (Settings.ShowTierSnake.Value && tierValue == MercTier.S)
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

        var tierValue = (MercTier)Settings.MercenaryTiers.GetValueOrDefault(mercName, 0);
        if (tierValue == MercTier.None) return;

        (string tierText, Color tierColor) = GetTierInfo(tierValue);

        var labelRect = label.GetClientRect();
        var textSize = ImGui.CalcTextSize(tierText);

        float centerX = labelRect.X + labelRect.Width / 2;
        float newX = centerX - textSize.X / 2;
        float newY = labelRect.Bottom - 5;

        var drawPos = new System.Numerics.Vector2(newX, newY);

        Graphics.DrawTextWithBackground(tierText, drawPos, tierColor, Settings.BackgroundColor.Value);
    }

    private (string, Color) GetTierInfo(MercTier tier) => tier switch
    {
        MercTier.S => ("S Tier", Settings.STierColor.Value),
        MercTier.A => ("A Tier", Settings.ATierColor.Value),
        MercTier.B => ("B Tier", Settings.BTierColor.Value),
        MercTier.C => ("C Tier", Settings.CTierColor.Value),
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
            if (!IsMercenary(mon)) continue;

            var isHired = IsHiredMerc(mon);
            var merScreenPos = GameController.IngameState.Camera.WorldToScreen(mon.PosNum);
            if (merScreenPos.Y < 0) continue;

            var name = mon.RenderName;
            var stats = Settings.UseLiveStatInference.Value
                ? GetMercStatTypes(mon, name)
                : (name != null ? MercenaryStats.GetValueOrDefault(name) : null);
            var level = Settings.UseLiveStatInference.Value ? GetMercLevel(mon) : null;

            mon.TryGetComponent<Actor>(out var actor);
            mon.TryGetComponent<Life>(out var life);
            mon.TryGetComponent<Buffs>(out var buffs);

            var centerX = merScreenPos.X;

            if (isHired)
            {
                if (!Settings.ShowHiredMercOverlays.Value) continue;

                if (Settings.ShowLinkStatus.Value)
                    DrawLevelAndLinkStatus(centerX, mon, level, merScreenPos.Y, lineHeight);
                else if (Settings.ShowMercLevel.Value && level is { } hiredLevel)
                    DrawTextAbove(centerX, $"Lvl {hiredLevel}", Settings.DefaultSkillColor.Value, merScreenPos.Y, lineHeight);

                if (life != null && Settings.ShowHiredMercHpBar.Value && life.MaxHP > 0)
                    DrawSimpleBar(centerX, merScreenPos.Y, life.CurHP, life.MaxHP, Settings.HpBarColor.Value, barHeight);

                if (actor != null)
                {
                    if (Settings.ShowHiredMercActionPanel.Value)
                        DrawHiredMercActionPanel(actor, buffs, centerX, merScreenPos.Y + lineHeight, lineHeight);
                    else if (Settings.ShowSkillCooldowns.Value)
                        DrawHiredMercCooldowns(actor, centerX, merScreenPos.Y + lineHeight, lineHeight, 0);
                }

                continue;
            }

            if (Settings.ShowEntityOverlays.Value)
            {
                if (stats != null && Settings.ShowEntityFrames.Value)
                    DrawEntityFrames(centerX, merScreenPos.Y, stats);

                var labelTop = merScreenPos.Y;
                if (name != null && Settings.ShowEntityTier.Value)
                {
                    var tierValue = (MercTier)Settings.MercenaryTiers.GetValueOrDefault(name, 0);
                    if (tierValue != MercTier.None)
                    {
                        var (tierText, tierColor) = GetTierInfo(tierValue);
                        labelTop = DrawTextAbove(centerX, tierText, tierColor, labelTop, lineHeight);
                    }
                }
                if (Settings.ShowMercLevel.Value && level is { } mercLevel)
                    labelTop = DrawTextAbove(centerX, $"Lvl {mercLevel}", Settings.DefaultSkillColor.Value, labelTop, lineHeight);

                if (life != null && Settings.ShowHpBar.Value && life.MaxHP > 0)
                    DrawSimpleBar(centerX, merScreenPos.Y, life.CurHP, life.MaxHP, Settings.HpBarColor.Value, barHeight);

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

    private void DrawCenteredText(float centerX, float y, string text, Color color, Color? background = null)
    {
        var textSize = ImGui.CalcTextSize(text);
        var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, y);
        Graphics.DrawTextWithBackground(text, drawPos, color, background ?? Settings.BackgroundColor.Value);
    }

    private void DrawSkillRow(float centerX, float y, string marker, Color markerColor, string name, Color nameColor)
    {
        var markerSize = ImGui.CalcTextSize(marker);
        var nameSize = ImGui.CalcTextSize(name);
        const float glyphGap = 2f;
        const float padding = 2f;
        var totalWidth = markerSize.X + glyphGap + nameSize.X;
        var height = Math.Max(markerSize.Y, nameSize.Y);
        var x = centerX - totalWidth / 2;

        var bgRect = new RectangleF(x - padding, y - padding, totalWidth + padding * 2, height + padding * 2);
        Graphics.DrawBox(bgRect, Settings.BackgroundColor.Value);

        var transparent = new Color(0, 0, 0, 0);
        Graphics.DrawTextWithBackground(marker, new System.Numerics.Vector2(x, y), markerColor, transparent);
        Graphics.DrawTextWithBackground(name, new System.Numerics.Vector2(x + markerSize.X + glyphGap, y), nameColor, transparent);
    }

    private static Color Desaturate(Color c)
    {
        var gray = (byte)((c.R + c.G + c.B) / 3);
        const float amount = 0.65f;
        return new Color(
            (byte)(c.R * (1 - amount) + gray * amount),
            (byte)(c.G * (1 - amount) + gray * amount),
            (byte)(c.B * (1 - amount) + gray * amount),
            c.A);
    }

    private void DrawLevelAndLinkStatus(float centerX, Entity mon, int? level, float currentTop, float lineHeight)
    {
        var linked = _flameLink != null && _flameLink.IsLinked(mon);
        var linkText = linked ? "Linked" : "Unlinked";
        var linkColor = linked ? Settings.LinkedColor.Value : Settings.UnlinkedColor.Value;

        var levelText = Settings.ShowMercLevel.Value && level is { } hiredLevel ? $"Lvl {hiredLevel}" : "";
        var levelSize = string.IsNullOrEmpty(levelText) ? default : ImGui.CalcTextSize(levelText);
        var linkSize = ImGui.CalcTextSize(linkText);

        const float gap = 6f;
        var levelGap = levelSize.X > 0 ? gap : 0f;
        var totalWidth = levelSize.X + levelGap + linkSize.X;
        var startX = centerX - totalWidth / 2;
        var rowY = currentTop - Math.Max(levelSize.Y, linkSize.Y) - 2;

        if (levelSize.X > 0)
            Graphics.DrawTextWithBackground(levelText, new System.Numerics.Vector2(startX, rowY), Settings.DefaultSkillColor.Value, Settings.BackgroundColor.Value);

        Graphics.DrawTextWithBackground(linkText, new System.Numerics.Vector2(startX + levelSize.X + levelGap, rowY), linkColor, Settings.BackgroundColor.Value);
    }

    private void DrawHiredMercActionPanel(Actor actor, Buffs buffs, float centerX, float startY, float lineHeight)
    {
        var line = 0;

        if (actor.ActorSkills != null)
        {
            foreach (var skill in actor.ActorSkills)
            {
                if (!IsDisplayableSkill(skill)) continue;

                var auraName = GetSkillDisplayName(skill);
                var isAura = IsAutoDetectedAura(skill, skill.EffectsPerLevel);

                if (!isAura) continue;

                var activeBuff = FindActiveAuraBuff(buffs, skill, auraName);

                var isActive = activeBuff != null;
                var timerText = isActive && activeBuff.MaxTime > 0 && activeBuff.MaxTime < PermanentBuffTime
                    ? $" {activeBuff.Timer:F1}s"
                    : "";

                var text = isActive ? $">> {auraName}{timerText} <<" : $"- {auraName}";
                var color = isActive ? Settings.AuraActiveColor.Value : new Color(0.45f, 0.45f, 0.45f, 1f);
                DrawCenteredText(centerX, startY + lineHeight * line, text, color);
                line++;
            }

            if (Settings.ShowSkillCooldowns.Value)
                line = DrawHiredMercCooldowns(actor, centerX, startY, lineHeight, line);
        }

        var usingSkill = (actor.Action & ActionFlags.UsingAbility) != 0;
        var isMoving = (actor.Action & ActionFlags.Moving) != 0 && !usingSkill;

        if (usingSkill && actor.CurrentAction?.Skill != null)
        {
            var skillName = GetSkillDisplayName(actor.CurrentAction.Skill);
            var text = $"> {skillName}";
            var textSize = ImGui.CalcTextSize(text);
            var drawPos = new System.Numerics.Vector2(centerX - textSize.X / 2, startY + lineHeight * line);
            var textRect = new RectangleF(drawPos.X - 2, drawPos.Y - 1, textSize.X + 4, textSize.Y + 2);
            Graphics.DrawFrame(textRect, new Color(0.2f, 1.0f, 0.4f, 1f), 1);
            Graphics.DrawTextWithBackground(text, drawPos, new Color(0.2f, 1.0f, 0.4f, 1f), Settings.BackgroundColor.Value);
            line++;
        }
        else if (isMoving)
        {
            DrawCenteredText(centerX, startY + lineHeight * line, "- Moving...", new Color(0.6f, 0.6f, 0.6f, 1f));
            line++;
        }

        if (Settings.ShowHiredMercBuffs.Value && buffs?.BuffsList != null)
        {
            foreach (var buff in buffs.BuffsList)
            {
                var buffName = buff.DisplayName ?? buff.Name;
                if (string.IsNullOrWhiteSpace(buffName)) continue;

                var isHighlighted = MatchesFilter(buffName);
                if (!isHighlighted) continue;

                var timerText = buff.MaxTime > 0 && buff.MaxTime < PermanentBuffTime ? $" {buff.Timer:F1}s" : "";
                DrawCenteredText(centerX, startY + lineHeight * line, $">> {buffName}{timerText} <<", Settings.AuraActiveColor.Value);
                line++;
            }
        }
    }

    private int DrawHiredMercCooldowns(Actor actor, float centerX, float startY, float lineHeight, int line)
    {
        if (actor.ActorSkills == null) return line;

        foreach (var skill in actor.ActorSkills)
        {
            if (!IsDisplayableSkill(skill)) continue;
            if (skill.IsChanneling) continue;
            if (!skill.IsOnCooldown) continue;

            var auraName = GetSkillDisplayName(skill);
            if (IsAutoDetectedAura(skill, skill.EffectsPerLevel)) continue;

            var cooldownInfo = skill.CooldownInfo?.SkillCooldowns is { Count: > 0 }
                ? skill.CooldownInfo.SkillCooldowns[0] : null;

            var cdRemaining = Math.Max(0f, cooldownInfo?.Remaining ?? skill.Cooldown);
            var cdTotal = skill.Cooldown;
            var cdText = cooldownInfo != null
                ? $" [{cdRemaining:F1}s]"
                : skill.Cooldown > 0 ? $" [{skill.Cooldown:F1}s]" : " [cd]";

            var text = $"{auraName}{cdText}";
            var textSize = ImGui.CalcTextSize(text);
            var rowY = startY + lineHeight * line;

            var remainingFraction = cdTotal > 0
                ? Math.Clamp((float)cdRemaining / cdTotal, 0f, 1f)
                : 1f;

            switch ((CooldownBarStyle)Settings.SkillCooldownBarStyle.Value)
            {
                case CooldownBarStyle.BelowText:
                    DrawCooldownBarBelow(centerX, rowY, text, textSize, remainingFraction);
                    line += 2;
                    break;
                case CooldownBarStyle.FillBackground:
                    DrawCooldownBarFill(centerX, rowY, text, textSize, remainingFraction);
                    line++;
                    break;
                default:
                    DrawCooldownBarInline(centerX, rowY, text, textSize, remainingFraction);
                    line++;
                    break;
            }
        }

        return line;
    }

    private void DrawCooldownBarInline(float centerX, float rowY, string text, System.Numerics.Vector2 textSize, float remainingFraction)
    {
        const float barWidth = 64f;
        const float barHeight = 8f;
        const float gap = 6f;
        var rowWidth = textSize.X + gap + barWidth;
        var rowX = centerX - rowWidth / 2;

        DrawCooldownBarText(text, new System.Numerics.Vector2(rowX, rowY), false);
        DrawCdBar(new RectangleF(rowX + textSize.X + gap, rowY + (textSize.Y - barHeight) / 2, barWidth, barHeight), remainingFraction);
    }

    private void DrawCooldownBarBelow(float centerX, float rowY, string text, System.Numerics.Vector2 textSize, float remainingFraction)
    {
        const float barWidth = 120f;
        const float barHeight = 8f;

        DrawCooldownBarText(text, new System.Numerics.Vector2(centerX - textSize.X / 2, rowY), false);
        DrawCdBar(new RectangleF(centerX - barWidth / 2, rowY + textSize.Y + 3, barWidth, barHeight), remainingFraction);
    }

    private void DrawCooldownBarFill(float centerX, float rowY, string text, System.Numerics.Vector2 textSize, float remainingFraction)
    {
        const float padding = 1f;
        var bgRect = new RectangleF(centerX - textSize.X / 2 - padding, rowY - padding, textSize.X + padding * 2, textSize.Y + padding * 2);
        Graphics.DrawBox(bgRect, new Color(0, 0, 0, 200));

        if (remainingFraction > 0f)
            Graphics.DrawBox(FractionRect(bgRect, remainingFraction), new Color(0.25f, 0.55f, 0.85f, 0.5f));

        Graphics.DrawFrame(bgRect, new Color(0.35f, 0.55f, 0.75f, 0.9f), 1);

        DrawCooldownBarText(text, new System.Numerics.Vector2(centerX - textSize.X / 2, rowY), true);
    }

    private void DrawCooldownBarText(string text, System.Numerics.Vector2 drawPos, bool onFill)
    {
        var color = onFill ? new Color(1f, 1f, 1f, 1f) : new Color(0.35f, 0.8f, 0.45f, 1f);
        var background = onFill ? new Color(0, 0, 0, 0) : Settings.BackgroundColor.Value;
        Graphics.DrawTextWithBackground(text, drawPos, color, background);
    }

    private void DrawCdBar(RectangleF barRect, float remainingFraction)
    {
        Graphics.DrawBox(barRect, new Color(0, 0, 0, 180));
        Graphics.DrawFrame(barRect, new Color(0.35f, 0.55f, 0.75f, 0.9f), 1);

        if (remainingFraction > 0f)
        {
            Graphics.DrawBox(FractionRect(barRect, remainingFraction), new Color(0.3f, 0.7f, 1f, 0.9f));
        }
    }

    private static RectangleF FractionRect(RectangleF rect, float fraction) =>
        new(rect.X, rect.Y, rect.Width * fraction, rect.Height);

    private void DrawSimpleBar(float centerX, float startY, int cur, int max, Color fillColor, float barHeight)
    {
        if (max <= 0) return;

        const float barWidth = 120f;
        var barX = centerX - barWidth / 2;
        var bgRect = new RectangleF(barX, startY, barWidth, barHeight);

        Graphics.DrawBox(bgRect, new Color(0, 0, 0, 180));

        var fillWidth = barWidth * Math.Min(1f, (float)cur / max);
        Graphics.DrawBox(new RectangleF(barX, startY, fillWidth, barHeight), fillColor);
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

            var isHighlighted = MatchesFilter(buffName);

            if (!isHighlighted) continue;

            var timerText = Settings.ShowAuraTimers.Value && buff.MaxTime > 0 && buff.MaxTime < PermanentBuffTime ? $" {buff.Timer:F1}s" : "";
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
        foreach (var skill in actor.ActorSkills.Where(IsDisplayableSkill))
        {
            var skillName = GetSkillDisplayName(skill);

            var isWanted = MatchesFilter(skillName);
            var isBad = MatchesBadFilter(skillName);

            var isAura = IsAutoDetectedAura(skill, skill.EffectsPerLevel);

            if (!isWanted && !isBad && !isAura && !Settings.ShowAllSkills.Value) continue;

            if (isAura && Settings.SeparateAuraDisplay.Value) continue;

            var attrColor = GetSkillColorForSkill(skill, skillName);
            var rowY = startY + lineHeight * line;

            if (isBad)
            {
                DrawSkillRow(centerX, rowY, "-", Settings.BadSkillColor.Value, skillName, Desaturate(attrColor));
                line++;
                continue;
            }

            if (isWanted)
            {
                DrawSkillRow(centerX, rowY, "+", Settings.HighlightSkillColor.Value, skillName, Settings.HighlightSkillColor.Value);
                line++;
                continue;
            }

            var labeledName = skillName;
            var showHighlight = isAura && !Settings.SeparateAuraDisplay.Value;
            if (showHighlight) labeledName = $">> {skillName} <<";

            var skillColor = showHighlight ? Settings.HighlightSkillColor.Value : attrColor;
            Color? background = showHighlight ? new Color(attrColor.R, attrColor.G, attrColor.B, (byte)155) : null;
            DrawCenteredText(centerX, rowY, labeledName, skillColor, background);
            line++;
        }

        return startY + lineHeight * line;
    }

    private void DrawEntityAuras(Actor actor, Buffs buffs, float centerX, float startY, float lineHeight)
    {
        if (actor.ActorSkills == null) return;
        var line = 0;

        foreach (var skill in actor.ActorSkills.Where(IsDisplayableSkill))
        {
            var auraName = GetSkillDisplayName(skill);

            if (!IsAutoDetectedAura(skill, skill.EffectsPerLevel)) continue;

            var activeBuff = FindActiveAuraBuff(buffs, skill, auraName);

            var isActive = activeBuff != null;
            var timerText = Settings.ShowAuraTimers.Value && isActive && activeBuff.MaxTime > 0 && activeBuff.MaxTime < PermanentBuffTime
                ? $" {activeBuff.Timer:F1}s"
                : "";

            var isWanted = MatchesFilter(auraName);
            var isBad = MatchesBadFilter(auraName);
            var rowY = startY + lineHeight * line;

            if (isBad)
            {
                DrawSkillRow(centerX, rowY, "-", Settings.BadSkillColor.Value, $"{auraName}{timerText}", Desaturate(Settings.AuraActiveColor.Value));
                line++;
                continue;
            }

            if (isWanted)
            {
                DrawSkillRow(centerX, rowY, "+", Settings.HighlightSkillColor.Value, $"{auraName}{timerText}", Settings.HighlightSkillColor.Value);
                line++;
                continue;
            }

            var displayText = isActive ? $">> {auraName}{timerText} <<" : $"- {auraName}";
            var textColor = isActive ? Settings.AuraActiveColor.Value : Settings.AuraInactiveColor.Value;
            DrawCenteredText(centerX, rowY, displayText, textColor);
            line++;
        }
    }

    private bool IsAutoDetectedAura(ActorSkill skill, GrantedEffectsPerLevel effects)
    {
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

    private void DrawOffScreenIndicators()
    {
        if (!Settings.ShowOffScreenIndicators.Value) return;
        if (!GameController.EntityListWrapper.ValidEntitiesByType.TryGetValue(EntityType.Monster, out var monsters)) return;

        var windowRect = GameController.Window.GetWindowRectangleTimeCache;
        var center = new System.Numerics.Vector2(windowRect.X + windowRect.Width / 2f, windowRect.Y + windowRect.Height / 2f);

        foreach (var mon in monsters)
        {
            if (mon.DistancePlayer > Settings.MaxIndicatorDistance.Value) continue;
            if (!IsMercenary(mon)) continue;

            var screenPos = GameController.IngameState.Camera.WorldToScreen(mon.PosNum);
            if (screenPos.X >= windowRect.X && screenPos.X <= windowRect.Right &&
                screenPos.Y >= windowRect.Y && screenPos.Y <= windowRect.Bottom) continue;

            var dir = screenPos - center;
            if (dir.LengthSquared() < 1f) continue;
            dir = System.Numerics.Vector2.Normalize(dir);

            var target = ClipRayToRect(center, dir, windowRect, 42f);
            if (target == null) continue;

            var name = mon.RenderName;
            var window = GameController.IngameState.IngameUi.MercenaryEncounterWindow;
            var stats = GetIndicatorStats(mon, name, window);
            var color = stats is { Count: > 0 }
                ? GetColorForStat(stats[0])
                : mon.Metadata.Contains("Allied", StringComparison.Ordinal)
                    ? Settings.HpBarColor.Value
                    : Settings.DefaultSkillColor.Value;

            var tip = target.Value + dir * 12f;
            var perp = new System.Numerics.Vector2(-dir.Y, dir.X);
            Graphics.DrawConvexPolyFilled(
                [tip, target.Value - dir * 8f + perp * 8f, target.Value - dir * 8f - perp * 8f],
                color);

            var label = string.IsNullOrWhiteSpace(name) ? "Mercenary" : name;
            var cls = GetIndicatorClass(mon, name, window);
            var lineHeight = ImGui.GetTextLineHeight();

            var arrowBase = target.Value - dir * 12f;
            var perp2 = new System.Numerics.Vector2(-dir.Y, dir.X) * 14f;
            var drawX = arrowBase.X + perp2.X;
            var drawY = arrowBase.Y + perp2.Y;

            Graphics.DrawTextWithBackground(label, new System.Numerics.Vector2(drawX, drawY), Color.White, Settings.BackgroundColor.Value);

            var distText = $"{mon.DistancePlayer:F0}m";
            var subText = string.IsNullOrWhiteSpace(cls) ? distText : $"{cls}  {distText}";
            Graphics.DrawTextWithBackground(subText,
                new System.Numerics.Vector2(drawX, drawY + lineHeight), color, Settings.BackgroundColor.Value);
        }
    }

    private static System.Numerics.Vector2? ClipRayToRect(System.Numerics.Vector2 origin, System.Numerics.Vector2 dir, RectangleF rect, float inset)
    {
        var minX = rect.X + inset;
        var maxX = rect.Right - inset;
        var minY = rect.Y + inset;
        var maxY = rect.Bottom - inset;

        float tMin = 0f;
        float tMax = float.MaxValue;

        if (Math.Abs(dir.X) > 1e-6f)
        {
            var t1 = (minX - origin.X) / dir.X;
            var t2 = (maxX - origin.X) / dir.X;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
        }
        else if (origin.X < minX || origin.X > maxX) return null;

        if (Math.Abs(dir.Y) > 1e-6f)
        {
            var t1 = (minY - origin.Y) / dir.Y;
            var t2 = (maxY - origin.Y) / dir.Y;
            if (t1 > t2) (t1, t2) = (t2, t1);
            tMin = Math.Max(tMin, t1);
            tMax = Math.Min(tMax, t2);
        }
        else if (origin.Y < minY || origin.Y > maxY) return null;

        if (tMin > tMax || tMax < 0) return null;
        return origin + dir * (tMin > 0 ? tMin : tMax);
    }

    private List<StatType> GetIndicatorStats(Entity mon, string name, Element window)
    {
        if (name != null)
        {
            var hardcoded = MercenaryStats.GetValueOrDefault(name);
            if (hardcoded is { Count: > 0 }) return hardcoded;
        }

        if (mon.TryGetComponent<Stats>(out var statsComp) && GetStatInt(statsComp, GameStat.Level) > 0)
        {
            var live = GetMercStatTypes(mon, name);
            if (live is { Count: > 0 }) return live;
        }

        if (window != null && window.IsVisible)
        {
            var fromWindow = FindWindowAttribute(window);
            if (fromWindow is { Count: > 0 }) return fromWindow;
        }

        if (mon.TryGetComponent<Actor>(out var actor) && actor.ActorSkills != null)
        {
            var fromSkills = actor.ActorSkills
                .Where(s => !string.IsNullOrWhiteSpace(s.Name))
                .Select(s => GetSkillStatType(s, GetSkillDisplayName(s)))
                .Where(a => a != null)
                .GroupBy(a => a)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .Cast<StatType>()
                .ToList();
            if (fromSkills.Count > 0) return fromSkills;
        }

        return null;
    }

    private static readonly Dictionary<string, string> ClassByMetadata = new Dictionary<string, string>();

    private static string GetMetadataClass(Entity mon)
    {
        var meta = mon.Metadata;
        var slash = meta.LastIndexOf('/');
        return slash >= 0 ? meta.Substring(slash + 1) : meta;
    }

    private string GetIndicatorClass(Entity mon, string name, Element window)
    {
        if (!string.IsNullOrWhiteSpace(name) && MercenaryStats.ContainsKey(name)) return name;

        var metaClass = GetMetadataClass(mon);
        if (ClassByMetadata.TryGetValue(metaClass, out var cached)) return cached;

        if (window != null && window.IsVisible && !string.IsNullOrWhiteSpace(name))
        {
            var cls = FindOfferArchetype(window, name);
            if (cls != null)
            {
                ClassByMetadata[metaClass] = cls;
                return cls;
            }
        }

        return null;
    }

    private static List<StatType> ParseStatTokens(string t)
    {
        var tokens = t.Split(new[] { ' ', '/', '+', '&' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return null;

        var stats = new List<StatType>(tokens.Length);
        foreach (var token in tokens)
        {
            switch (token)
            {
                case "Str": stats.Add(StatType.Str); break;
                case "Dex": stats.Add(StatType.Dex); break;
                case "Int": stats.Add(StatType.Int); break;
                default: return null;
            }
        }
        return stats;
    }

    private static IEnumerable<string> CollectTexts(Element root, int maxDepth, int maxLength)
    {
        var texts = new List<string>();
        void Walk(Element e, int depth)
        {
            if (e == null || depth > maxDepth) return;
            var t = e.Text?.Trim();
            if (!string.IsNullOrEmpty(t) && t.Length < maxLength) texts.Add(t);
            foreach (var c in e.Children) Walk(c, depth + 1);
        }
        foreach (var c in root.Children) Walk(c, 0);
        return texts;
    }

    private static string FindOfferArchetype(Element window, string expectedName)
    {
        var texts = CollectTexts(window, 6, 60).ToList();
        var idx = texts.FindIndex(t => string.Equals(t, expectedName, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return null;

        for (var i = idx + 1; i < texts.Count; i++)
        {
            var t = texts[i];
            if (ParseStatTokens(t) is { Count: > 0 }) continue;
            if (t.StartsWith("Lvl ", StringComparison.Ordinal) || t.StartsWith("Wager", StringComparison.Ordinal)) continue;
            if (t.Length <= 24) return t;
        }
        return null;
    }

    private StatType? GetSkillStatType(ActorSkill skill, string skillName)
    {
        if (_skillAttributeMap.TryGetValue(skillName, out var attr)) return attr;
        if (skill.IsCry) return StatType.Str;
        if (skill.IsMine || skill.IsTrap) return StatType.Dex;
        return null;
    }

    private static List<StatType> FindWindowAttribute(Element window)
    {
        foreach (var t in CollectTexts(window, 6, 30))
        {
            var stats = ParseStatTokens(t);
            if (stats is { Count: > 0 }) return stats;
        }
        return new List<StatType>();
    }

    #endregion
}
