using System.Linq;
using ExileCore.Shared.Nodes;
using ImGuiNET;
using SharpDX;

namespace MercScanner;

public class MercScannerSettingsDrawer
{
    private readonly MercScannerSettings _settings;

    public MercScannerSettingsDrawer(MercScannerSettings settings)
    {
        _settings = settings;
    }

    public void Draw()
    {
        if (ImGui.CollapsingHeader("General Highlighting", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawToggleNode("Highlight Mercenary Items", _settings.HighlightMercenaryItems);
            ImGui.Spacing();
            DrawColorNode("Str Color", _settings.StrColor);
            DrawColorNode("Dex Color", _settings.DexColor);
            DrawColorNode("Int Color", _settings.IntColor);
        }

        if (ImGui.CollapsingHeader("Mercenary Tiers", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawToggleNode("Show Tier Text", _settings.ShowTierText);
            ImGui.Spacing();
            DrawColorNode("S Tier Color", _settings.STierColor);
            DrawColorNode("A Tier Color", _settings.ATierColor);
            DrawColorNode("B Tier Color", _settings.BTierColor);
            DrawColorNode("C Tier Color", _settings.CTierColor);
            
            ImGui.Separator();
            ImGui.Text("Set tier by number. A key is provided for reference.");
            ImGui.TextColored(new System.Numerics.Vector4(1, 1, 1, 0.7f), "Key: 0=None, 1=S, 2=A, 3=B, 4=C");
            ImGui.Separator();
            
            ImGui.Columns(2, "MercenaryTierColumns", false);
            ImGui.SetColumnWidth(0, 200);
            
            foreach (var mercName in _settings.MercenaryTiers.Keys.OrderBy(x => x).ToList())
            {
                ImGui.Text(mercName);
                ImGui.NextColumn();

                int value = _settings.MercenaryTiers[mercName];
                if (ImGui.SliderInt($"##{mercName}TierSlider", ref value, 0, 4))
                {
                    _settings.MercenaryTiers[mercName] = value;
                }
                ImGui.NextColumn();
            }
            ImGui.Columns(1);
        }

        if (ImGui.CollapsingHeader("Idle Mercenary Skill Display", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawToggleNode("Show All Skills", _settings.ShowAllSkills);
            ImGui.Spacing();
            DrawColorNode("Highlight Skill Color", _settings.HighlightSkillColor);
            DrawColorNode("Default Skill Color", _settings.DefaultSkillColor);
            DrawColorNode("Background Color", _settings.BackgroundColor);
            ImGui.Separator();
            DrawContentNode("Auras", _settings.Auras);
        }
    }

    private void DrawToggleNode(string label, ToggleNode node)
    {
        bool value = node.Value;
        if (ImGui.Checkbox(label, ref value))
        {
            node.Value = value;
        }
    }
    
    private void DrawColorNode(string label, ColorNode node)
    {
        var colorSharpDx = node.Value.ToVector4();
        var colorNumerics = new System.Numerics.Vector4(colorSharpDx.X, colorSharpDx.Y, colorSharpDx.Z, colorSharpDx.W);

        if (ImGui.ColorEdit4(label, ref colorNumerics, ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar))
        {
            node.Value = new Color(colorNumerics.X, colorNumerics.Y, colorNumerics.Z, colorNumerics.W);
        }
    }
    
    private void DrawContentNode(string label, ContentNode<TextNode> node)
    {
        ImGui.Text(label);
        for (int i = node.Content.Count - 1; i >= 0; i--)
        {
            ImGui.PushID($"Aura_{i}");
            if (ImGui.Button("Remove"))
            {
                node.Content.RemoveAt(i);
            }
            else
            {
                ImGui.SameLine();
                string value = node.Content[i].Value;
                if (ImGui.InputText("##AuraText", ref value, 100))
                {
                    node.Content[i].Value = value;
                }
            }
            ImGui.PopID();
        }

        if (ImGui.Button("Add item"))
        {
            node.Content.Add(new TextNode(""));
        }
    }
}