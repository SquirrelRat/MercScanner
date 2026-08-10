using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Nodes;

namespace MercScanner;

// Fresh, serializable settings. No [Menu] attributes, no CustomNode/ContentNode — the settings page is
// drawn entirely by MercScanner.DrawSettings() (see MercScannerSettingsUi.cs), and every type here is a
// plain value node that Newtonsoft round-trips without touching any delegate.
public class MercScannerSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new(false);

    // General
    public ToggleNode IgnoreFullscreenPanels { get; set; } = new(false);
    public ToggleNode IgnoreLargePanels { get; set; } = new(false);

    // Tiers
    public ToggleNode ShowTierText { get; set; } = new(true);
    public ColorNode STierColor { get; set; } = new(System.Drawing.Color.MediumPurple.ToSharpDx());
    public ColorNode ATierColor { get; set; } = new(System.Drawing.Color.LawnGreen.ToSharpDx());
    public ColorNode BTierColor { get; set; } = new(System.Drawing.Color.Yellow.ToSharpDx());
    public ColorNode CTierColor { get; set; } = new(System.Drawing.Color.Red.ToSharpDx());
    public Dictionary<string, int> MercenaryTiers { get; set; } = new();
    public ToggleNode ShowTierFrame { get; set; } = new(false);
    public RangeNode<int> TierFrameFillOpacity { get; set; } = new(20, 0, 100);
    public ToggleNode ShowTierSnake { get; set; } = new(false);
    public RangeNode<float> TierSnakeSpeed { get; set; } = new(3f, 0.5f, 8f);
    public RangeNode<float> TierSnakeIntensity { get; set; } = new(1f, 0.3f, 2f);

    // Unhired mercs
    public ToggleNode ShowEntityOverlays { get; set; } = new(true);
    public ToggleNode ShowHpBar { get; set; } = new(true);
    public ColorNode HpBarColor { get; set; } = new(System.Drawing.Color.LawnGreen.ToSharpDx());
    public ToggleNode ShowEntityFrames { get; set; } = new(true);
    public ToggleNode ShowEntityTier { get; set; } = new(true);
    public ToggleNode UseLiveStatInference { get; set; } = new(true);
    public ToggleNode ShowMercLevel { get; set; } = new(true);
    public RangeNode<int> MaxMercDistance { get; set; } = new(80, 10, 200);
    public ToggleNode ShowOffScreenIndicators { get; set; } = new(false);
    public RangeNode<int> MaxIndicatorDistance { get; set; } = new(500, 50, 5000);

    // Name labels (ground item labels on mercenary drops)
    public ToggleNode HighlightMercenary { get; set; } = new(true);
    public ColorNode StrColor { get; set; } = new(System.Drawing.Color.FromArgb(210, 0, 0).ToSharpDx());
    public ColorNode DexColor { get; set; } = new(System.Drawing.Color.FromArgb(0, 210, 0).ToSharpDx());
    public ColorNode IntColor { get; set; } = new(System.Drawing.Color.FromArgb(0, 128, 255).ToSharpDx());

    // Skills & auras
    public ToggleNode ShowAllSkills { get; set; } = new(true);
    public ColorNode HighlightSkillColor { get; set; } = new(System.Drawing.Color.LawnGreen.ToSharpDx());
    public ColorNode DefaultSkillColor { get; set; } = new(System.Drawing.Color.White.ToSharpDx());
    public ColorNode MonsterSkillColor { get; set; } = new(System.Drawing.Color.MediumPurple.ToSharpDx());
    public List<string> SkillFilter { get; set; } = new();
    public List<string> BadSkillFilter { get; set; } = new();
    public ColorNode BadSkillColor { get; set; } = new(System.Drawing.Color.Red.ToSharpDx());
    public Dictionary<string, int> SupportRatings { get; set; } = new();
    public Dictionary<string, int> SupportSkillOverrides { get; set; } = new();
    public ToggleNode SeparateAuraDisplay { get; set; } = new(true);
    public ToggleNode AutoDetectAuras { get; set; } = new(true);
    public ToggleNode ShowAuraTimers { get; set; } = new(true);
    public ColorNode AuraActiveColor { get; set; } = new(System.Drawing.Color.Cyan.ToSharpDx());
    public ColorNode AuraInactiveColor { get; set; } = new(System.Drawing.Color.DimGray.ToSharpDx());
    public ColorNode BackgroundColor { get; set; } = new(System.Drawing.Color.Black.ToSharpDx());
    public ToggleNode HighlightEncounterPanelBorders { get; set; } = new(true);

    // Hired mercs
    public ToggleNode ShowHiredMercOverlays { get; set; } = new(true);
    public ToggleNode ShowHiredMercHpBar { get; set; } = new(true);
    public ToggleNode ShowHiredMercActionPanel { get; set; } = new(true);
    public ToggleNode ShowSkillCooldowns { get; set; } = new(true);
    public RangeNode<int> SkillCooldownBarStyle { get; set; } = new(2, 0, 2);
    public ToggleNode ShowHiredMercBuffs { get; set; } = new(false);

    // Flame link
    public ToggleNode ShowLinkStatus { get; set; } = new(true);
    public ColorNode LinkedColor { get; set; } = new(System.Drawing.Color.Yellow.ToSharpDx());
    public ColorNode UnlinkedColor { get; set; } = new(System.Drawing.Color.Red.ToSharpDx());
    public ToggleNode AutoCastFlameLink { get; set; } = new(false);
    public HotkeyNodeV2 FlameLinkKey { get; set; } = new(Keys.None);
    public ToggleNode RequireSkillReady { get; set; } = new(true);
    public ToggleNode RestoreCursor { get; set; } = new(true);
    public RangeNode<int> CursorSettleMs { get; set; } = new(60, 10, 300);
    public RangeNode<int> CursorRestoreMs { get; set; } = new(40, 0, 300);
    public RangeNode<int> CastMarginMs { get; set; } = new(50, 0, 500);
    public RangeNode<int> CastGapMs { get; set; } = new(400, 50, 3000);
    public RangeNode<int> RelinkCooldownMs { get; set; } = new(2000, 500, 10000);
    public RangeNode<float> MaxCastDistance { get; set; } = new(100f, 10f, 1000f);
    public ToggleNode DontCastInTown { get; set; } = new(true);
    public ToggleNode DontCastWithPanelsOpen { get; set; } = new(true);
}
