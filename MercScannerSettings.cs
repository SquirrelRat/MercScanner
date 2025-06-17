using ExileCore.Shared.Helpers;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using SharpDX;
using Color = System.Drawing.Color;

namespace MercScanner;

public class MercScannerSettings : ISettings
{
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode IgnoreLargePanels { get; set; } = new ToggleNode(false);
    public ToggleNode IgnoreFullscreenPanels { get; set; } = new ToggleNode(false);
    public ColorNode HighlightSkillColor { get; set; } = new ColorNode(Color.Green.ToSharpDx());
    public ColorNode DefaultSkillColor { get; set; } = new ColorNode(Color.White.ToSharpDx());
    public ColorNode BackgroundColor { get; set; } = new ColorNode(Color.Black.ToSharpDx());
    public ToggleNode ShowAllSkills { get; set; } = new ToggleNode(true);
    public ContentNode<TextNode> Auras { get; set; } = new ContentNode<TextNode>() { EnableControls = true, UseFlatItems = true, ItemFactory = () => new TextNode("") };
}