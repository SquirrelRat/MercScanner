using System;
using SharpDX;

namespace MercScanner;

// Gradient rating for mercenary support gems. Supports only matter in the context
// of the skill they support (a support can be perfect on one skill and a brick on
// another), so every rating resolves through a per-(skill, support) override first
// and falls back to the support's global rating, then Neutral.
public enum SupportTier
{
    Bad = 0,
    Poor = 1,
    Neutral = 2,
    Good = 3,
    Great = 4,
    Perfect = 5,
}

public partial class MercScanner
{
    internal static readonly SupportTier[] AllSupportTiers =
        (SupportTier[])Enum.GetValues(typeof(SupportTier));

    // Red -> orange -> grey -> lime -> teal -> purple gradient.
    internal static Color TierColor(SupportTier tier) => tier switch
    {
        SupportTier.Bad => new Color(0.9f, 0.15f, 0.15f, 1f),
        SupportTier.Poor => new Color(0.95f, 0.55f, 0.1f, 1f),
        SupportTier.Neutral => new Color(0.55f, 0.55f, 0.55f, 1f),
        SupportTier.Good => new Color(0.4f, 0.9f, 0.3f, 1f),
        SupportTier.Great => new Color(0.15f, 0.85f, 0.85f, 1f),
        SupportTier.Perfect => new Color(0.75f, 0.3f, 0.95f, 1f),
        _ => new Color(0.55f, 0.55f, 0.55f, 1f),
    };

    // Combo override ("SkillName|SupportName") wins, then the support's global
    // rating, then Neutral. Unknown supports are never assumed good or bad.
    internal SupportTier ResolveSupportRating(string skillName, string supportName)
    {
        var overrideKey = $"{skillName}|{supportName}";
        if (Settings.SupportSkillOverrides.TryGetValue(overrideKey, out var overrideValue))
            return (SupportTier)overrideValue;

        if (Settings.SupportRatings.TryGetValue(supportName, out var rating))
            return (SupportTier)rating;

        return SupportTier.Neutral;
    }
}
