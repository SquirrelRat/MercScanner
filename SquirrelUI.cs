// House UI helpers - drop-in, no edits needed.
// Copy this file unchanged into any plugin's source folder. It lives in the global
// namespace so every plugin compiles its own independent copy.
using System;
using System.Collections.Generic;
using System.Numerics;
using Hexa.NET.ImGui;

public static class SquirrelUi
{
    internal static uint U32(byte r, byte g, byte b, byte a) => (uint)(a << 24 | b << 16 | g << 8 | r);

    private static readonly uint Accent = U32(124, 92, 255, 255);
    private static readonly uint AccentHover = U32(139, 110, 255, 255);
    private static readonly uint AccentWash = U32(124, 92, 255, 38);
    private static readonly uint Track = U32(42, 46, 53, 255);
    private static readonly uint KnobWhite = U32(255, 255, 255, 255);
    private static readonly Vector4 AccentSoft = new Vector4(0.66f, 0.61f, 1.0f, 1.0f);
    private static readonly Vector4 MutedV4 = new Vector4(0.54f, 0.56f, 0.60f, 1.0f);

    public static void PushSettingsStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12, 10));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 4));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 6));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.GrabRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.TabRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 8f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 10f);

        ImGui.PushStyleColor(ImGuiCol.Text, U32(232, 235, 238, 255));
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, U32(138, 143, 153, 255));
        ImGui.PushStyleColor(ImGuiCol.WindowBg, U32(16, 18, 22, 255));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, U32(22, 25, 32, 255));
        ImGui.PushStyleColor(ImGuiCol.Border, U32(35, 38, 43, 255));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, U32(30, 33, 39, 255));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, U32(41, 46, 54, 255));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, U32(51, 56, 66, 255));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, KnobWhite);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, KnobWhite);
        ImGui.PushStyleColor(ImGuiCol.Button, U32(41, 46, 56, 255));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, U32(61, 66, 79, 255));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, U32(77, 84, 99, 255));
        ImGui.PushStyleColor(ImGuiCol.Header, U32(124, 92, 255, 89));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, U32(124, 92, 255, 64));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, U32(124, 92, 255, 115));
        ImGui.PushStyleColor(ImGuiCol.Separator, U32(35, 38, 43, 255));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, U32(16, 18, 22, 255));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, U32(61, 66, 77, 255));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, U32(124, 92, 255, 140));
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, U32(124, 92, 255, 230));
    }

    public static void PopSettingsStyle()
    {
        ImGui.PopStyleColor(22);
        ImGui.PopStyleVar(9);
    }

    public static void SectionHeader(string name)
    {
        // Subtitles use accent-soft so they stand out from setting labels and values.
        ImGui.Dummy(new Vector2(0, 8));
        ImGui.TextColored(AccentSoft, name.ToUpperInvariant());
        ImGui.Dummy(new Vector2(0, 4));
    }

    public static void HelpMarker(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered()) ImGui.SetTooltip(text);
    }

    // Action button whose text lands on the 44px label gutter of the v2 row grid.
    public static bool ActionButton(string label, string id, int kind = 0, string help = null)
    {
        var start = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(start + new Vector2(34, 0));
        bool clicked = Button(label, id, kind);
        if (help != null)
        {
            var textEnd = start.X + 34 + 10 + ImGui.CalcTextSize(label).X;
            ImGui.SetCursorScreenPos(new Vector2(textEnd + 8, start.Y));
            HelpMarker(help);
        }
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + ImGui.GetTextLineHeight() + 8));
        return clicked;
    }

    public static bool Button(string label, string id, int kind = 0)
    {
        var cursor = ImGui.GetCursorScreenPos();
        float w = ImGui.CalcTextSize(label).X + 20;
        float h = ImGui.GetTextLineHeight() + 8;
        bool clicked = DrawButtonAt(cursor, w, h, label, kind, id);
        ImGui.SetCursorScreenPos(new Vector2(cursor.X, cursor.Y + h));
        return clicked;
    }

    public static int ButtonRow(params (string label, string id, int kind)[] buttons)
    {
        var cursor = ImGui.GetCursorScreenPos();
        float h = ImGui.GetTextLineHeight() + 8;
        float x = cursor.X;
        int clickedIdx = -1;
        for (int i = 0; i < buttons.Length; i++)
        {
            float w = ImGui.CalcTextSize(buttons[i].label).X + 20;
            if (DrawButtonAt(new Vector2(x, cursor.Y), w, h, buttons[i].label, buttons[i].kind, buttons[i].id))
                clickedIdx = i;
            x += w + 8;
        }
        ImGui.SetCursorScreenPos(new Vector2(cursor.X, cursor.Y + h));
        return clickedIdx;
    }

    private static bool DrawButtonAt(Vector2 pos, float w, float h, string label, int kind, string id)
    {
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton("##btn_" + id, new Vector2(w, h));
        bool clicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();
        bool active = ImGui.IsItemActive();
        var dl = ImGui.GetWindowDrawList();
        uint fill;
        uint border = U32(58, 51, 79, 255);
        if (kind == 1) { fill = hovered || active ? AccentHover : Accent; border = fill; }
        else if (kind == 2) { fill = hovered || active ? U32(150, 45, 52, 255) : U32(100, 30, 35, 255); border = fill; }
        else { fill = hovered || active ? U32(43, 33, 64, 255) : U32(27, 22, 38, 255); if (hovered) border = Accent; }
        dl.AddRectFilled(pos, pos + new Vector2(w, h), fill, 4f);
        dl.AddRect(pos, pos + new Vector2(w, h), border, 4f, 1f);
        ImGui.SetCursorScreenPos(pos + new Vector2(10, 4));
        ImGui.TextColored(new Vector4(1f, 1f, 1f, 1f), label);
        return clicked;
    }

    public static void HouseHeader(string name, string version)
    {
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        dl.AddRectFilled(pos, pos + new Vector2(w, 32), U32(124, 92, 255, 26), 6f);
        ImGui.SetCursorScreenPos(pos + new Vector2(12, 7));
        ImGui.TextColored(AccentSoft, name);
        if (!string.IsNullOrEmpty(version) && version != "0.0")
        {
            ImGui.SetCursorScreenPos(pos + new Vector2(12 + ImGui.CalcTextSize(name).X + 8, 7));
            ImGui.TextColored(MutedV4, "v" + version);
        }
        var mark = "SQUIRRELRAT";
        var mw = ImGui.CalcTextSize(mark).X;
        ImGui.SetCursorScreenPos(pos + new Vector2(w - mw - 12, 7));
        ImGui.TextColored(new Vector4(0.486f, 0.360f, 1.0f, 1.0f), mark);
        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + 32 + 6));
    }

    public static int ConfirmRow(string message, string id)
    {
        // Returns 1 = confirm, 2 = cancel, 0 = untouched. Absolute positions avoid
        // the cursor drift caused by SameLine after a custom Button.
        var rowPos = ImGui.GetCursorScreenPos();
        var rowH = ImGui.GetTextLineHeight() + 8;
        var msgW = ImGui.CalcTextSize(message).X;
        ImGui.SetCursorScreenPos(rowPos + new Vector2(0, 3));
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.2f, 1f), message);
        ImGui.SetCursorScreenPos(rowPos + new Vector2(msgW + 10, 3));
        bool yes = Button("Confirm", "confirm_" + id, 1);
        ImGui.SetCursorScreenPos(rowPos + new Vector2(msgW + 104, 3));
        bool no = Button("Cancel", "cancel_" + id, 0);
        ImGui.SetCursorScreenPos(new Vector2(rowPos.X, rowPos.Y + rowH));
        return yes ? 1 : no ? 2 : 0;
    }

    public static void HouseTitle(string name)
    {
        // Header band without version or author mark - for panels inside a plugin
        // (editors, sub-windows) where that chrome would be misleading.
        var dl = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X;
        dl.AddRectFilled(pos, pos + new Vector2(w, 32), U32(124, 92, 255, 26), 6f);
        ImGui.SetCursorScreenPos(pos + new Vector2(12, 7));
        ImGui.TextColored(AccentSoft, name);
        ImGui.SetCursorScreenPos(new Vector2(pos.X, pos.Y + 32 + 6));
    }

    public static bool PillToggle(string label, bool value, string id)
    {
        float lineH = ImGui.GetTextLineHeight();
        float rowH = lineH + 6f;
        float avail = ImGui.GetContentRegionAvail().X;
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##pill_" + id, new Vector2(avail, rowH));
        bool clicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
            dl.AddRectFilled(cursor, cursor + new Vector2(avail, rowH), U32(124, 92, 255, 22), 6f);
        ImGui.SetCursorScreenPos(cursor + new Vector2(6, 3));
        ImGui.Text(label);
        float labelW = ImGui.GetItemRectSize().X;
        float pillX = cursor.X + 6 + labelW + 10;
        float pillY = cursor.Y + (rowH - 16) / 2;
        uint fill = value ? (hovered ? AccentHover : Accent) : Track;
        dl.AddRectFilled(new Vector2(pillX, pillY), new Vector2(pillX + 30, pillY + 16), fill, 8f);
        float knobCx = value ? pillX + 30 - 2 - 6 : pillX + 2 + 6;
        dl.AddCircleFilled(new Vector2(knobCx, pillY + 8), 6f, KnobWhite);
        ImGui.SetCursorScreenPos(new Vector2(cursor.X, cursor.Y + rowH));
        if (clicked) value = !value;
        return value;
    }

    private static readonly HashSet<string> _editingSliders = new();
    private static readonly HashSet<string> _valuePress = new();
    private static string _focusEditId;

    public static bool ModernSlider(string label, ref int value, int min, int max, string id)
    {
        float floatValue = value;
        if (!ModernSliderFloat(label, ref floatValue, min, max, id)) return false;
        value = (int)Math.Round((double)floatValue);
        return true;
    }

    private static bool ModernSliderFloat(string label, ref float value, float min, float max, string id)
    {
        var labelSize = ImGui.CalcTextSize(label);
        var valueText = ((int)value).ToString();
        var valueSize = ImGui.CalcTextSize(valueText);
        const float editBoxW = 64f;
        bool editing = _editingSliders.Contains(id);
        float valueAreaW = editing ? editBoxW : valueSize.X;
        float lineH = ImGui.GetTextLineHeight();
        float textH = Math.Max(labelSize.Y, Math.Max(valueSize.Y, lineH));
        float height = textH + 6f;
        var cursor = ImGui.GetCursorScreenPos();
        float totalWidth = ImGui.GetContentRegionAvail().X;

        var labelPos = new Vector2(cursor.X, cursor.Y + (height - lineH) * 0.5f);
        var valuePos = new Vector2(cursor.X + totalWidth - valueAreaW, cursor.Y + (height - lineH) * 0.5f);
        var valueHitMin = valuePos - new Vector2(6, 3);
        var valueHitMax = valuePos + valueSize + new Vector2(6, 3);
        if (ImGui.IsMouseHoveringRect(valueHitMin, valueHitMax) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            _editingSliders.Add(id);
            _valuePress.Add(id);
            _focusEditId = id;
        }
        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left)) _valuePress.Remove(id);

        float lineStartX = cursor.X + labelSize.X + 12f;
        float lineEndX = cursor.X + totalWidth - valueAreaW - 12f;
        float lineLength = lineEndX - lineStartX;
        float lineY = cursor.Y + height * 0.5f;

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(0, height + 2));
        ImGui.PushStyleColor(ImGuiCol.Button, 0);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0);
        ImGui.PushStyleColor(ImGuiCol.Text, 0);
        bool clicked = ImGui.InvisibleButton("##sld_" + id, new Vector2(totalWidth, height));
        ImGui.PopStyleColor(4);
        ImGui.PopStyleVar(1);

        bool isActive = ImGui.IsItemActive();
        bool isHovered = ImGui.IsItemHovered();
        float oldValue = value;
        var dl = ImGui.GetWindowDrawList();
        dl.AddText(labelPos, U32(232, 235, 238, 255), label);

        if (editing)
        {
            float editH = lineH;
            var editPos = new Vector2(cursor.X + totalWidth - editBoxW, cursor.Y + (height - editH) * 0.5f);
            ImGui.SetCursorScreenPos(editPos);
            ImGui.SetNextItemWidth(editBoxW);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6, 0));
            if (_focusEditId == id)
            {
                ImGui.SetKeyboardFocusHere();
                _focusEditId = null;
            }
            ImGui.InputFloat("##edit_" + id, ref value, 0f, 0f, "%g", ImGuiInputTextFlags.EnterReturnsTrue);
            ImGui.PopStyleVar(1);
            if (ImGui.IsItemDeactivated()) _editingSliders.Remove(id);
        }
        else
        {
            dl.AddText(valuePos, U32(183, 174, 240, 255), valueText);
        }

        if (lineLength <= 0 || max <= min) return false;

        float norm = Math.Max(0f, Math.Min(1f, (value - min) / (max - min)));
        float dotX = lineStartX + norm * lineLength;
        dl.AddLine(new Vector2(lineStartX, lineY), new Vector2(lineEndX, lineY), Track, 3f);
        if (dotX > lineStartX)
            dl.AddLine(new Vector2(lineStartX, lineY), new Vector2(dotX, lineY), isActive ? AccentHover : Accent, 3f);
        dl.AddCircleFilled(new Vector2(dotX, lineY), isActive ? 6.5f : 5.5f, KnobWhite);

        if (!editing && !_valuePress.Contains(id) && (isActive || (clicked && isHovered)))
        {
            float mx = ImGui.GetMousePos().X;
            float nn = Math.Max(0f, Math.Min(1f, (mx - lineStartX) / lineLength));
            value = min + nn * (max - min);
        }
        return value != oldValue;
    }

    public static bool PillMini(bool value, string id, float rowH = 0)
    {
        var cursor = ImGui.GetCursorScreenPos();
        float h = rowH > 16 ? rowH : 16;
        ImGui.InvisibleButton("##mini_" + id, new Vector2(30, h));
        bool clicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        uint fill = value ? (hovered ? AccentHover : Accent) : Track;
        float pillY = cursor.Y + (h - 16) / 2;
        dl.AddRectFilled(new Vector2(cursor.X, pillY), new Vector2(cursor.X + 30, pillY + 16), fill, 8f);
        float knobCx = value ? cursor.X + 30 - 2 - 6 : cursor.X + 2 + 6;
        dl.AddCircleFilled(new Vector2(knobCx, pillY + 8), 6f, KnobWhite);
        if (clicked) value = !value;
        return value;
    }

    public static bool SidebarRow(string label, string sub, bool selected, string id, float rightPad = 0)
    {
        var cursor = ImGui.GetCursorScreenPos();
        float w = ImGui.GetContentRegionAvail().X - rightPad;
        float h = ImGui.GetTextLineHeight() + 10;
        ImGui.InvisibleButton("##side_" + id, new Vector2(w, h));
        bool clicked = ImGui.IsItemClicked();
        bool hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        if (selected)
        {
            dl.AddRectFilled(cursor, cursor + new Vector2(w, h), AccentWash, 4f);
            dl.AddRectFilled(new Vector2(cursor.X + w - 3, cursor.Y + 4), cursor + new Vector2(w, h - 4), Accent, 0f);
        }
        else if (hovered)
        {
            dl.AddRectFilled(cursor, cursor + new Vector2(w, h), U32(124, 92, 255, 18), 4f);
        }
        float subW = sub == null ? 0 : ImGui.CalcTextSize(sub).X;
        string shown = label;
        float maxNameW = w - 10 - subW - 10;
        if (maxNameW > 10 && ImGui.CalcTextSize(shown).X > maxNameW)
        {
            while (shown.Length > 1 && ImGui.CalcTextSize(shown + "..").X > maxNameW)
                shown = shown.Substring(0, shown.Length - 1);
            shown += "..";
        }
        ImGui.SetCursorScreenPos(cursor + new Vector2(10, 5));
        var safeShown = shown.Replace("%", "%%");
        if (selected) ImGui.Text(safeShown);
        else ImGui.TextColored(MutedV4, safeShown);
        if (sub != null)
        {
            ImGui.SetCursorScreenPos(cursor + new Vector2(w - subW - 10, 5));
            ImGui.TextColored(MutedV4, sub.Replace("%", "%%"));
        }
        ImGui.SetCursorScreenPos(new Vector2(cursor.X, cursor.Y + h));
        return clicked;
    }

    public static bool ColorSwatch(Vector4 color01, string id, float rowH = 0)
    {
        var cursor = ImGui.GetCursorScreenPos();
        float h = rowH > 16 ? rowH : 16;
        ImGui.InvisibleButton("##sw_" + id, new Vector2(16, h));
        bool clicked = ImGui.IsItemClicked();
        var dl = ImGui.GetWindowDrawList();
        float swY = cursor.Y + (h - 16) / 2;
        dl.AddRectFilled(new Vector2(cursor.X, swY), new Vector2(cursor.X + 16, swY + 16), ColorConvert(color01), 3f);
        return clicked;
    }

    public static uint ColorConvert(Vector4 c)
    {
        byte r = (byte)(c.X * 255);
        byte g = (byte)(c.Y * 255);
        byte b = (byte)(c.Z * 255);
        byte a = (byte)(c.W * 255);
        return U32(r, g, b, a);
    }

    public static Vector4 Muted => MutedV4;

    public static void FilterRow(string label, string id, string[] options, ref int current)
    {
        // FilterChip leaves the cursor at the end of its chip on the same line.
        // Restore it to the start of the next row, otherwise successive filter
        // groups run together on one line.
        var start = ImGui.GetCursorScreenPos();
        var rowH = ImGui.GetTextLineHeight() + 6;
        ImGui.TextColored(Muted, label + ":");
        for (var i = 0; i < options.Length; i++)
        {
            ImGui.SameLine();
            if (FilterChip(options[i], current == i, id + i)) current = i;
        }
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + rowH));
    }

    public static bool FilterChip(string label, bool active, string id)
    {
        var cursor = ImGui.GetCursorScreenPos();
        var width = ImGui.CalcTextSize(label).X + 12;
        var height = ImGui.GetTextLineHeight() + 4;
        var size = new Vector2(width, height);
        ImGui.InvisibleButton("##chip_" + id, size);
        var clicked = ImGui.IsItemClicked();
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        var fill = active ? Accent : hovered ? U32(61, 66, 79, 255) : U32(30, 33, 39, 255);
        dl.AddRectFilled(cursor, cursor + size, fill, 8f);
        ImGui.SetCursorScreenPos(cursor + new Vector2(6, 2));
        ImGui.TextColored(active ? new Vector4(1f, 1f, 1f, 1f) : Muted, label);
        ImGui.SetCursorScreenPos(new Vector2(cursor.X + width, cursor.Y));
        return clicked;
    }
}


