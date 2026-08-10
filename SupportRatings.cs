using System;
using SharpDX;

namespace MercScanner;

public enum SupportTier
{
    D = 0,
    C = 1,
    B = 2,
    A = 3,
    S = 4,
}

public partial class MercScanner
{
    internal static readonly SupportTier[] AllSupportTiers =
        (SupportTier[])Enum.GetValues(typeof(SupportTier));

    internal static Color TierColor(SupportTier tier) => tier switch
    {
        SupportTier.S => new Color(0.75f, 0.3f, 0.95f, 1f),
        SupportTier.A => new Color(0.4f, 0.9f, 0.3f, 1f),
        SupportTier.B => new Color(0.95f, 0.85f, 0.15f, 1f),
        SupportTier.C => new Color(0.95f, 0.55f, 0.1f, 1f),
        SupportTier.D => new Color(0.9f, 0.15f, 0.15f, 1f),
        _ => new Color(0.9f, 0.15f, 0.15f, 1f),
    };

    internal static string TierLetter(SupportTier tier) => tier switch
    {
        SupportTier.S => "S",
        SupportTier.A => "A",
        SupportTier.B => "B",
        SupportTier.C => "C",
        SupportTier.D => "D",
        _ => "?",
    };

    internal SupportTier? ResolveSupportRating(string skillName, string supportName)
    {
        var overrideKey = $"{skillName}|{supportName}";
        if (Settings.SupportSkillOverrides.TryGetValue(overrideKey, out var overrideValue))
            return (SupportTier)overrideValue;

        if (Settings.SupportRatings.TryGetValue(supportName, out var rating))
            return (SupportTier)rating;

        return null;
    }
}
