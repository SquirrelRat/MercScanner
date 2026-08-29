using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;

namespace MercScanner;

public sealed class MercPanelScanner
{
    private const float RowWidth = 539f;
    private const float RowHeight = 44f;
    private const float RowWidthTolerance = 0.15f;
    private const float RowHeightTolerance = 0.2f;

    private const int RefreshMs = 250;

    public sealed record SupportGem(string Name, RectangleF Rect);

    public sealed record SkillRow(string Name, RectangleF Rect, IReadOnlyList<SupportGem> Supports);

    private readonly MercScanner _plugin;
    private Element _cachedWindow;
    private List<SkillRow> _cachedRows;
    private readonly Stopwatch _cacheAge = Stopwatch.StartNew();

    public MercPanelScanner(MercScanner plugin)
    {
        _plugin = plugin;
    }

    public IReadOnlyList<SkillRow> Read()
    {
        var window = _plugin.GameController.IngameState.IngameUi.MercenaryEncounterWindow;
        if (window == null || !window.IsValid || !window.IsVisible)
        {
            _cachedWindow = null;
            _cachedRows = null;
            return Array.Empty<SkillRow>();
        }

        if (!ReferenceEquals(window, _cachedWindow) || _cachedRows == null || _cacheAge.ElapsedMilliseconds > RefreshMs)
        {
            _cachedWindow = window;
            _cachedRows = Scan(window);
            _cacheAge.Restart();
        }

        return _cachedRows;
    }

    private static List<SkillRow> Scan(Element window)
    {
        var rows = new List<SkillRow>();

        foreach (var row in Descendants(window, 8).Where(IsSkillRow))
        {
            var name = FirstText(row, 3);
            if (string.IsNullOrWhiteSpace(name)) continue;

            var supports = Descendants(row, 4)
                .Where(IsSupportGem)
                .Select(gem => new SupportGem(FirstText(gem.Tooltip, 6), gem.GetClientRect()))
                .Where(g => !string.IsNullOrWhiteSpace(g.Name))
                .OrderBy(g => g.Rect.X)
                .ToList();

            rows.Add(new SkillRow(name, row.GetClientRect(), supports));
        }

        return rows;
    }

    private static bool IsSkillRow(Element e)
    {
        try
        {
            var rect = e.GetClientRect();
            // The encounter UI scales with the client's UI scale. Use a wider
            // proportional tolerance rather than rejecting valid scaled rows.
            return Math.Abs(rect.Width - RowWidth) <= RowWidth * RowWidthTolerance &&
                   Math.Abs(rect.Height - RowHeight) <= RowHeight * RowHeightTolerance;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSupportGem(Element e)
    {
        try
        {
            var rect = e.GetClientRect();
            return rect.Width >= 38f && rect.Width <= 46f &&
                   rect.Height >= 38f && rect.Height <= 44f;
        }
        catch
        {
            return false;
        }
    }

    private static string FirstText(Element root, int depth)
    {
        if (root == null || !root.IsValid) return null;

        foreach (var e in Descendants(root, depth))
        {
            try
            {
                var text = e.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(text) && text.Length < 60)
                    return text;
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<Element> Descendants(Element root, int depth)
    {
        if (root == null) yield break;
        yield return root;
        if (depth <= 0) yield break;

        foreach (var child in root.Children ?? Enumerable.Empty<Element>())
        {
            foreach (var descendant in Descendants(child, depth - 1))
            {
                yield return descendant;
            }
        }
    }
}
