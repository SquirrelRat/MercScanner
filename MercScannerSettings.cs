using System.Collections.Generic;
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
    public ContentNode<TextNode> Auras { get; set; } = new ContentNode<TextNode>() { EnableControls = true, UseFlatItems = true, ItemFactory = () => new TextNode("") };
    
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

    public ToggleNode ShowEntityOverlays { get; set; } = new ToggleNode(true);
    public ToggleNode ShowHpBar { get; set; } = new ToggleNode(true);
    public ToggleNode ShowEsBar { get; set; } = new ToggleNode(true);
    public ColorNode HpBarColor { get; set; } = new ColorNode(System.Drawing.Color.LawnGreen.ToSharpDx());
    public ColorNode EsBarColor { get; set; } = new ColorNode(System.Drawing.Color.DodgerBlue.ToSharpDx());
    public ToggleNode ShowEntityFrames { get; set; } = new ToggleNode(true);
    public ToggleNode ShowEntityTier { get; set; } = new ToggleNode(true);
    public RangeNode<int> MaxMercDistance { get; set; } = new RangeNode<int>(80, 10, 200);

    public ColorNode MonsterSkillColor { get; set; } = new ColorNode(System.Drawing.Color.MediumPurple.ToSharpDx());

    public ToggleNode ShowHiredMercOverlays { get; set; } = new ToggleNode(true);
    public ToggleNode ShowHiredMercHpBar { get; set; } = new ToggleNode(true);
    public ToggleNode ShowHiredMercEsBar { get; set; } = new ToggleNode(true);
    public ToggleNode ShowHiredMercActionPanel { get; set; } = new ToggleNode(true);
}