using System.Collections.Generic;
using System.Windows.Forms;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;

namespace MercScanner;

public class MercScannerSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode IgnoreLargePanels { get; set; } = new ToggleNode(false);
    public ToggleNode IgnoreFullscreenPanels { get; set; } = new ToggleNode(false);

    public ColorNode HighlightSkillColor { get; set; } = new ColorNode(System.Drawing.Color.LawnGreen.ToSharpDx());
    public ColorNode DefaultSkillColor { get; set; } = new ColorNode(System.Drawing.Color.White.ToSharpDx());
    public ColorNode BackgroundColor { get; set; } = new ColorNode(System.Drawing.Color.Black.ToSharpDx());
    public ToggleNode ShowAllSkills { get; set; } = new ToggleNode(true);
    public ToggleNode SeparateAuraDisplay { get; set; } = new ToggleNode(true);
    public ColorNode AuraActiveColor { get; set; } = new ColorNode(System.Drawing.Color.Cyan.ToSharpDx());
    public ColorNode AuraInactiveColor { get; set; } = new ColorNode(System.Drawing.Color.DimGray.ToSharpDx());
    public ToggleNode ShowAuraTimers { get; set; } = new ToggleNode(true);
    public ToggleNode AutoDetectAuras { get; set; } = new ToggleNode(true);

    public ContentNode<TextNode> SkillFilter { get; set; } = new ContentNode<TextNode>() { EnableControls = true, UseFlatItems = true, ItemFactory = () => new TextNode("") };

    public ToggleNode HighlightMercenary { get; set; } = new ToggleNode(true);
    public ColorNode StrColor { get; set; } = new ColorNode(System.Drawing.Color.FromArgb(210, 0, 0).ToSharpDx());
    public ColorNode DexColor { get; set; } = new ColorNode(System.Drawing.Color.FromArgb(0, 210, 0).ToSharpDx());
    public ColorNode IntColor { get; set; } = new ColorNode(System.Drawing.Color.FromArgb(0, 128, 255).ToSharpDx());

    public Dictionary<string, int> MercenaryTiers { get; set; } = new();

    public ToggleNode ShowTierText { get; set; } = new ToggleNode(true);
    public ColorNode STierColor { get; set; } = new ColorNode(System.Drawing.Color.MediumPurple.ToSharpDx());
    public ColorNode ATierColor { get; set; } = new ColorNode(System.Drawing.Color.LawnGreen.ToSharpDx());
    public ColorNode BTierColor { get; set; } = new ColorNode(System.Drawing.Color.Yellow.ToSharpDx());
    public ColorNode CTierColor { get; set; } = new ColorNode(System.Drawing.Color.Red.ToSharpDx());

    public ToggleNode ShowTierFrame { get; set; } = new ToggleNode(false);
    public RangeNode<int> TierFrameFillOpacity { get; set; } = new RangeNode<int>(20, 0, 100);
    public ToggleNode ShowTierSnake { get; set; } = new ToggleNode(false);
    public RangeNode<float> TierSnakeSpeed { get; set; } = new RangeNode<float>(3f, 0.5f, 8f);
    public RangeNode<float> TierSnakeIntensity { get; set; } = new RangeNode<float>(1f, 0.3f, 2f);

    public ToggleNode ShowEntityOverlays { get; set; } = new ToggleNode(true);
    public ToggleNode ShowHpBar { get; set; } = new ToggleNode(true);
    public ColorNode HpBarColor { get; set; } = new ColorNode(System.Drawing.Color.LawnGreen.ToSharpDx());
    public ToggleNode ShowEntityFrames { get; set; } = new ToggleNode(true);
    public ToggleNode ShowEntityTier { get; set; } = new ToggleNode(true);
    public RangeNode<int> MaxMercDistance { get; set; } = new RangeNode<int>(80, 10, 200);

    public ColorNode MonsterSkillColor { get; set; } = new ColorNode(System.Drawing.Color.MediumPurple.ToSharpDx());

    public ToggleNode ShowHiredMercOverlays { get; set; } = new ToggleNode(true);
    public ToggleNode ShowHiredMercHpBar { get; set; } = new ToggleNode(true);
    public ToggleNode ShowHiredMercActionPanel { get; set; } = new ToggleNode(true);
    public ToggleNode ShowSkillCooldowns { get; set; } = new ToggleNode(true);
    public RangeNode<int> SkillCooldownBarStyle { get; set; } = new RangeNode<int>(2, 0, 2);
    public ToggleNode ShowHiredMercBuffs { get; set; } = new ToggleNode(false);

    public ToggleNode UseLiveStatInference { get; set; } = new ToggleNode(true);
    public ToggleNode ShowMercLevel { get; set; } = new ToggleNode(true);

    public ToggleNode ShowOffScreenIndicators { get; set; } = new ToggleNode(false);
    public RangeNode<int> MaxIndicatorDistance { get; set; } = new RangeNode<int>(500, 50, 5000);

    public ToggleNode ShowLinkStatus { get; set; } = new ToggleNode(true);
    public ColorNode LinkedColor { get; set; } = new ColorNode(System.Drawing.Color.Yellow.ToSharpDx());
    public ColorNode UnlinkedColor { get; set; } = new ColorNode(System.Drawing.Color.Red.ToSharpDx());

    public ToggleNode AutoCastFlameLink { get; set; } = new ToggleNode(false);
    public HotkeyNodeV2 FlameLinkKey { get; set; } = new HotkeyNodeV2(Keys.None);
    public ToggleNode RequireSkillReady { get; set; } = new ToggleNode(true);
    public RangeNode<int> CastMarginMs { get; set; } = new RangeNode<int>(50, 0, 500);
    public RangeNode<int> CastGapMs { get; set; } = new RangeNode<int>(400, 50, 3000);
    public RangeNode<int> CursorSettleMs { get; set; } = new RangeNode<int>(60, 10, 300);
    public RangeNode<int> CursorRestoreMs { get; set; } = new RangeNode<int>(40, 0, 300);
    public ToggleNode RestoreCursor { get; set; } = new ToggleNode(true);
    public RangeNode<float> MaxCastDistance { get; set; } = new RangeNode<float>(100f, 10f, 1000f);
    public ToggleNode DontCastInTown { get; set; } = new ToggleNode(true);
    public ToggleNode DontCastWithPanelsOpen { get; set; } = new ToggleNode(true);
}
