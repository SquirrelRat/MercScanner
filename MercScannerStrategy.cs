using System;
using System.Collections.Generic;

namespace MercScanner;

// Curated "auto-assigned strategy" preset, researched against the 3.29 Curse of
// the Allflame meta (official patch notes, poedb, reddit r/pathofexile and
// r/PathOfExileBuilds, mobalytics merc-bot guide, Odealo merc guide). Apply with
// the "Load Auto-Assigned Strategy" button in the Tiers settings tab.
public partial class MercScanner
{
    internal static readonly string[] DefaultGoodSkills =
    [
        // Auras (highest-value first — reservation-heavy ones shine on a merc)
        "Hatred",
        "Grace",
        "Haste",
        "Determination",
        "Zealotry",
        "Wrath",
        "Anger",
        "Malevolence",
        "Pride",
        "Aspect of the Spider",
        "Envy",
        "Precision",
        "Vitality",
        "Summon Skitterbots",
        "Purity of Ice",
        // Curses
        "Elemental Weakness",
        "Despair",
        "Conductivity",
        "Flammability",
        "Assassin's Mark",
        "Temporal Chains",
        // Utility / damage skills
        "Sigil of Power",
        "Flame Wall",
        "Void Sphere",
        "Wither",
        // 3.29-relevant archetype damage skills (Trarthan gems were buffed in 3.29.0)
        "Kinetic Blast of Clustering",
        "Ice Shot",
        "Vaal Ice Shot",
        "Spectral Helix of Trarthus",
        "Static Strike",
        "Frost Blades",
        "Bladefall of Trarthus",
        "Storm Call of Trarthus",
    ];

    internal static readonly string[] DefaultBadSkills =
    [
        // Known bad picks on mercs (3.29 testing): Kinetic Bolt bricks the
        // Kineticist AI so it stops using Kinetic Blast of Clustering; Icicle
        // Rain deals poor damage and interrupts Vaal Ice Shot on Manyshot.
        "Kinetic Bolt",
        "Icicle Rain",
        // Movement skills — 3.29.0 patched merc AI to not spam them, but they can
        // still mess with positioning, so keep them as a soft negative.
        "Dash",
        "Flame Dash",
        "Whirling Blades",
        "Leap Slam",
        "Blink Arrow",
        "Frostblink",
    ];

    // Base archetype tiers from community tier lists. "Infamous X" variants are
    // auto-derived one tier higher (S stays S). Archetypes not listed stay None.
    internal static readonly Dictionary<string, MercTier> DefaultArchetypeTiers = new()
    {
        ["Kineticist"] = MercTier.S,
        ["Withertouch"] = MercTier.S,
        ["Eruptor"] = MercTier.A,
        ["Fallen Reverend"] = MercTier.A,
        ["Flaming Charlatan"] = MercTier.A,
        ["Bladecaster"] = MercTier.A,
        ["Manyshot"] = MercTier.A,
        ["Blade Ambusher"] = MercTier.A,
        ["Cruel Mistress"] = MercTier.B,
        ["Stormhand"] = MercTier.B,
        ["Shattersword"] = MercTier.B,
        ["Combatant"] = MercTier.B,
        ["Swiftblade"] = MercTier.C,
        ["Bloodletter"] = MercTier.C,
        ["Storming Zealot"] = MercTier.C,
        ["Frosthand"] = MercTier.C,
        ["Flamehand"] = MercTier.C,
    };

    // Curated gradient ratings for support gems (3.29 meta research). Keyed by the
    // support gem's display name; anything not listed falls back to Neutral. Combo
    // nuances (a support that is perfect on one skill and a brick on another) live
    // in DefaultSupportSkillOverrides.
    internal static readonly Dictionary<string, int> DefaultSupportRatings = new()
    {
        // Core damage multipliers — great almost everywhere on a merc
        ["Elemental Damage with Attacks"] = (int)SupportTier.Great,
        ["Greater Elemental Damage with Attacks"] = (int)SupportTier.Perfect,
        ["Melee Physical Damage"] = (int)SupportTier.Good,
        ["Greater Melee Physical Damage"] = (int)SupportTier.Great,
        ["Physical as Extra"] = (int)SupportTier.Good,
        ["Greater Physical as Extra"] = (int)SupportTier.Great,
        ["Physical as Extra Chaos"] = (int)SupportTier.Good,
        ["Greater Physical as Extra Chaos"] = (int)SupportTier.Great,
        ["Critical Chance"] = (int)SupportTier.Good,
        ["Greater Critical Chance"] = (int)SupportTier.Great,
        ["Critical Damage"] = (int)SupportTier.Good,
        ["Greater Critical Damage"] = (int)SupportTier.Great,
        ["Concentrated Effect"] = (int)SupportTier.Good,
        ["Greater Concentrated Effect"] = (int)SupportTier.Great,
        ["Pulverise"] = (int)SupportTier.Good,
        ["Greater Pulverise"] = (int)SupportTier.Great,
        ["Multistrike"] = (int)SupportTier.Good,
        ["Greater Multistrike"] = (int)SupportTier.Great,
        ["Elemental Focus"] = (int)SupportTier.Good,
        ["Greater Elemental Focus"] = (int)SupportTier.Great,
        ["Faster Attacks"] = (int)SupportTier.Good,
        ["Greater Faster Attacks"] = (int)SupportTier.Great,
        ["Faster Casting"] = (int)SupportTier.Good,
        ["Greater Faster Casting"] = (int)SupportTier.Great,
        ["Cooldown Recovery"] = (int)SupportTier.Good,
        ["Greater Cooldown Recovery"] = (int)SupportTier.Great,
        // Projectile/clear tools
        ["Multiple Projectiles"] = (int)SupportTier.Great,
        ["Greater Multiple Projectiles"] = (int)SupportTier.Perfect,
        ["Return"] = (int)SupportTier.Great,
        ["Chain"] = (int)SupportTier.Good,
        ["Greater Chain"] = (int)SupportTier.Great,
        ["Pierce"] = (int)SupportTier.Good,
        ["Greater Pierce"] = (int)SupportTier.Great,
        ["Fork"] = (int)SupportTier.Good,
        ["Greater Fork"] = (int)SupportTier.Great,
        ["Slower Projectiles"] = (int)SupportTier.Good,
        ["Greater Slower Projectiles"] = (int)SupportTier.Great,
        ["Faster Projectiles"] = (int)SupportTier.Good,
        ["Greater Faster Projectiles"] = (int)SupportTier.Great,
        ["Arrow Nova"] = (int)SupportTier.Good,
        ["Added Cold"] = (int)SupportTier.Good,
        ["Greater Added Cold"] = (int)SupportTier.Great,
        ["Added Fire"] = (int)SupportTier.Good,
        ["Greater Added Fire"] = (int)SupportTier.Great,
        ["Added Lightning"] = (int)SupportTier.Good,
        ["Greater Added Lightning"] = (int)SupportTier.Great,
        ["Added Chaos"] = (int)SupportTier.Good,
        ["Greater Added Chaos"] = (int)SupportTier.Great,
        ["Cold Penetration"] = (int)SupportTier.Good,
        ["Greater Cold Penetration"] = (int)SupportTier.Great,
        ["Fire Penetration"] = (int)SupportTier.Good,
        ["Greater Fire Penetration"] = (int)SupportTier.Great,
        ["Lightning Penetration"] = (int)SupportTier.Good,
        ["Greater Lightning Penetration"] = (int)SupportTier.Great,
        ["Chaos Penetration"] = (int)SupportTier.Good,
        ["Greater Chaos Penetration"] = (int)SupportTier.Great,
        ["Hypothermia"] = (int)SupportTier.Good,
        ["Greater Hypothermia"] = (int)SupportTier.Great,
        // Traps / mines / totems
        ["Trap and Mine Damage"] = (int)SupportTier.Good,
        ["Greater Trap and Mine Damage"] = (int)SupportTier.Great,
        ["Multiple Traps"] = (int)SupportTier.Good,
        ["Multiple Totems"] = (int)SupportTier.Good,
        ["Throwing Speed"] = (int)SupportTier.Good,
        ["Greater Throwing Speed"] = (int)SupportTier.Great,
        // Physical / bleed / impale
        ["Maim"] = (int)SupportTier.Good,
        ["Impale Chance"] = (int)SupportTier.Good,
        ["Greater Impale Chance"] = (int)SupportTier.Great,
        ["Chance to Bleed"] = (int)SupportTier.Good,
        ["Greater Chance to Bleed"] = (int)SupportTier.Great,
        // DoT / ailment
        ["DoT Multiplier"] = (int)SupportTier.Good,
        ["Greater DoT Multiplier"] = (int)SupportTier.Great,
        ["Swift Affliction"] = (int)SupportTier.Good,
        ["Greater Swift Affliction"] = (int)SupportTier.Great,
        ["Wither on Hit"] = (int)SupportTier.Good,
        ["Greater Wither on Hit"] = (int)SupportTier.Great,
        ["Mirage Archer"] = (int)SupportTier.Good,
        ["Ailment Damage"] = (int)SupportTier.Good,
        ["Greater Ailment Damage"] = (int)SupportTier.Great,
        // Minion mercs
        ["Minion Damage"] = (int)SupportTier.Good,
        ["Greater Minion Damage"] = (int)SupportTier.Great,
        ["Minion Life"] = (int)SupportTier.Good,
        ["Greater Minion Life"] = (int)SupportTier.Great,
        ["Minion Caustic Death"] = (int)SupportTier.Good,
        // Utility
        ["Generosity"] = (int)SupportTier.Good,
        ["Greater Generosity"] = (int)SupportTier.Great,
        ["Second Wind"] = (int)SupportTier.Good,
        ["Fortify"] = (int)SupportTier.Good,
        ["Greater Fortify"] = (int)SupportTier.Great,
        ["More Duration"] = (int)SupportTier.Good,
        ["Greater More Duration"] = (int)SupportTier.Great,
        ["Warcry Speed"] = (int)SupportTier.Good,
        ["Greater Warcry Speed"] = (int)SupportTier.Great,
        ["Raging Cry"] = (int)SupportTier.Good,
        ["Greater Raging Cry"] = (int)SupportTier.Great,
        ["Infused Channelling"] = (int)SupportTier.Good,
        ["Greater Infused Channelling"] = (int)SupportTier.Great,
        // Consistently poor or brick-tier picks on mercs
        ["Deadly Ailments"] = (int)SupportTier.Bad,
        ["Knockback"] = (int)SupportTier.Poor,
        ["Less Duration"] = (int)SupportTier.Poor,
        ["Ironwood"] = (int)SupportTier.Poor,
        ["Greater Ironwood"] = (int)SupportTier.Poor,
        ["Ailment Effect"] = (int)SupportTier.Poor,
        ["Greater Ailment Effect"] = (int)SupportTier.Poor,
        ["Brittle Chance"] = (int)SupportTier.Poor,
        ["Shock Chance"] = (int)SupportTier.Poor,
        ["Greater Shock Chance"] = (int)SupportTier.Poor,
        ["Freeze Chance"] = (int)SupportTier.Poor,
        ["Greater Freeze Chance"] = (int)SupportTier.Poor,
        ["Ignite Chance"] = (int)SupportTier.Poor,
        ["Greater Ignite Chance"] = (int)SupportTier.Poor,
        ["Chance to Poison"] = (int)SupportTier.Poor,
        ["Greater Chance to Poison"] = (int)SupportTier.Poor,
        ["Rage on Hit"] = (int)SupportTier.Poor,
        ["Greater Rage on Hit"] = (int)SupportTier.Poor,
        ["Brutality"] = (int)SupportTier.Neutral,
    };

    // Combo-specific ratings: "SkillName|SupportName" -> tier. These override the
    // global rating when the same support appears under that skill, because a
    // support can be a perfect fit on one skill and actively brick another.
    internal static readonly Dictionary<string, int> DefaultSupportSkillOverrides = new()
    {
        // Kineticist: Returning Projectiles / Multiple Projectiles make Kinetic
        // Blast of Clustering the best-in-slot clear setup.
        ["Kinetic Blast of Clustering|Return"] = (int)SupportTier.Perfect,
        ["Kinetic Blast of Clustering|Multiple Projectiles"] = (int)SupportTier.Great,
        ["Kinetic Blast of Clustering|Greater Multiple Projectiles"] = (int)SupportTier.Perfect,
        // Manyshot: Ice Shot / Vaal Ice Shot love Returning Projectiles.
        ["Ice Shot|Return"] = (int)SupportTier.Perfect,
        ["Ice Shot|Elemental Damage with Attacks"] = (int)SupportTier.Great,
        ["Vaal Ice Shot|Return"] = (int)SupportTier.Perfect,
        ["Vaal Ice Shot|Cooldown Recovery"] = (int)SupportTier.Great,
        // Blade Ambusher: Spectral Helix of Trarthus trap setup.
        ["Spectral Helix of Trarthus|Multiple Traps"] = (int)SupportTier.Perfect,
        ["Spectral Helix of Trarthus|Trap and Mine Damage"] = (int)SupportTier.Great,
        ["Spectral Helix of Trarthus|Slower Projectiles"] = (int)SupportTier.Good,
        // Combatant: Static Strike / Frost Blades clear tools.
        ["Static Strike|Elemental Damage with Attacks"] = (int)SupportTier.Great,
        ["Static Strike|More Duration"] = (int)SupportTier.Great,
        ["Static Strike|Chain"] = (int)SupportTier.Good,
        ["Frost Blades|Return"] = (int)SupportTier.Perfect,
        ["Frost Blades|Elemental Damage with Attacks"] = (int)SupportTier.Great,
        ["Frost Blades|Chain"] = (int)SupportTier.Good,
        ["Frost Blades|Hypothermia"] = (int)SupportTier.Great,
        ["Frost Blades|Cold Penetration"] = (int)SupportTier.Good,
        // Eruptor: flame link / warcry merc.
        ["Vigilant Strike|Gilded Fortification"] = (int)SupportTier.Perfect,
        ["Volcanic Fissure of Snaking|Gilded Additional Fissures"] = (int)SupportTier.Great,
        // Brutality bricks chaos / conversion skills.
        ["Kinetic Blast of Clustering|Brutality"] = (int)SupportTier.Bad,
        ["Soulrend of Reaping|Brutality"] = (int)SupportTier.Bad,
        ["Wither|Brutality"] = (int)SupportTier.Bad,
        ["Void Sphere|Brutality"] = (int)SupportTier.Bad,
    };

    // Replaces the wanted/bad skill lists, the archetype tiers, and the support
    // ratings with the curated preset (including auto-derived "Infamous" variants).
    public void LoadAutoAssignedStrategy()
    {
        Settings.SkillFilter.Clear();
        Settings.SkillFilter.AddRange(DefaultGoodSkills);

        Settings.BadSkillFilter.Clear();
        Settings.BadSkillFilter.AddRange(DefaultBadSkills);

        foreach (var key in MercenaryStats.Keys)
            Settings.MercenaryTiers[key] = (int)MercTier.None;

        foreach (var (name, tier) in DefaultArchetypeTiers)
        {
            Settings.MercenaryTiers[name] = (int)tier;

            var infamous = "Infamous " + name;
            if (MercenaryStats.ContainsKey(infamous))
                Settings.MercenaryTiers[infamous] = (int)NextBetterTier(tier);
        }

        Settings.SupportRatings.Clear();
        foreach (var (name, rating) in DefaultSupportRatings)
            Settings.SupportRatings[name] = rating;

        Settings.SupportSkillOverrides.Clear();
        foreach (var (key, rating) in DefaultSupportSkillOverrides)
            Settings.SupportSkillOverrides[key] = rating;

        LogMessage(
            $"Auto-assigned strategy loaded: {Settings.SkillFilter.Count} wanted skills, " +
            $"{Settings.BadSkillFilter.Count} bad skills, {DefaultArchetypeTiers.Count} archetype tiers, " +
            $"{Settings.SupportRatings.Count} support ratings, {Settings.SupportSkillOverrides.Count} combo overrides.");
    }

    // True factory reset: clears every list, drops every tier to None, and wipes
    // the support ratings so everything falls back to Neutral grey.
    public void ZeroAllDefaults()
    {
        Settings.SkillFilter.Clear();
        Settings.BadSkillFilter.Clear();

        foreach (var key in MercenaryStats.Keys)
            Settings.MercenaryTiers[key] = (int)MercTier.None;

        Settings.SupportRatings.Clear();
        Settings.SupportSkillOverrides.Clear();

        LogMessage("Defaults restored: skill lists, archetype tiers and support ratings were zeroed.");
    }

    private static MercTier NextBetterTier(MercTier tier) => tier switch
    {
        MercTier.S => MercTier.S,
        MercTier.A => MercTier.S,
        MercTier.B => MercTier.A,
        MercTier.C => MercTier.B,
        _ => MercTier.None,
    };
}
