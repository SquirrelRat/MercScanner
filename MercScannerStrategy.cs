using System;
using System.Collections.Generic;

namespace MercScanner;

public partial class MercScanner
{
    internal static readonly string[] DefaultGoodSkills =
    [
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
        "Elemental Weakness",
        "Despair",
        "Conductivity",
        "Flammability",
        "Assassin's Mark",
        "Temporal Chains",
        "Sigil of Power",
        "Flame Wall",
        "Void Sphere",
        "Wither",
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
        "Kinetic Bolt",
        "Blast Rain",
        "Icicle Rain",
        "Dash",
        "Flame Dash",
        "Whirling Blades",
        "Leap Slam",
        "Blink Arrow",
        "Frostblink",
    ];

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

    internal static readonly Dictionary<string, int> DefaultSupportRatings = new()
    {
        ["Elemental Damage with Attacks"] = (int)SupportTier.A,
        ["Greater Elemental Damage with Attacks"] = (int)SupportTier.S,
        ["Melee Physical Damage"] = (int)SupportTier.B,
        ["Greater Melee Physical Damage"] = (int)SupportTier.A,
        ["Physical as Extra"] = (int)SupportTier.B,
        ["Greater Physical as Extra"] = (int)SupportTier.A,
        ["Physical as Extra Chaos"] = (int)SupportTier.B,
        ["Greater Physical as Extra Chaos"] = (int)SupportTier.A,
        ["Critical Chance"] = (int)SupportTier.B,
        ["Greater Critical Chance"] = (int)SupportTier.A,
        ["Critical Damage"] = (int)SupportTier.B,
        ["Greater Critical Damage"] = (int)SupportTier.A,
        ["Concentrated Effect"] = (int)SupportTier.B,
        ["Greater Concentrated Effect"] = (int)SupportTier.A,
        ["Pulverise"] = (int)SupportTier.B,
        ["Greater Pulverise"] = (int)SupportTier.A,
        ["Multistrike"] = (int)SupportTier.B,
        ["Greater Multistrike"] = (int)SupportTier.A,
        ["Elemental Focus"] = (int)SupportTier.B,
        ["Greater Elemental Focus"] = (int)SupportTier.A,
        ["Faster Attacks"] = (int)SupportTier.B,
        ["Greater Faster Attacks"] = (int)SupportTier.A,
        ["Faster Casting"] = (int)SupportTier.B,
        ["Greater Faster Casting"] = (int)SupportTier.A,
        ["Cooldown Recovery"] = (int)SupportTier.B,
        ["Greater Cooldown Recovery"] = (int)SupportTier.A,
        ["Multiple Projectiles"] = (int)SupportTier.A,
        ["Greater Multiple Projectiles"] = (int)SupportTier.S,
        ["Return"] = (int)SupportTier.A,
        ["Chain"] = (int)SupportTier.B,
        ["Greater Chain"] = (int)SupportTier.A,
        ["Pierce"] = (int)SupportTier.B,
        ["Greater Pierce"] = (int)SupportTier.A,
        ["Fork"] = (int)SupportTier.B,
        ["Greater Fork"] = (int)SupportTier.A,
        ["Slower Projectiles"] = (int)SupportTier.B,
        ["Greater Slower Projectiles"] = (int)SupportTier.A,
        ["Faster Projectiles"] = (int)SupportTier.B,
        ["Greater Faster Projectiles"] = (int)SupportTier.A,
        ["Arrow Nova"] = (int)SupportTier.B,
        ["Added Cold"] = (int)SupportTier.B,
        ["Greater Added Cold"] = (int)SupportTier.A,
        ["Added Fire"] = (int)SupportTier.B,
        ["Greater Added Fire"] = (int)SupportTier.A,
        ["Added Lightning"] = (int)SupportTier.B,
        ["Greater Added Lightning"] = (int)SupportTier.A,
        ["Added Chaos"] = (int)SupportTier.B,
        ["Greater Added Chaos"] = (int)SupportTier.A,
        ["Cold Penetration"] = (int)SupportTier.B,
        ["Greater Cold Penetration"] = (int)SupportTier.A,
        ["Fire Penetration"] = (int)SupportTier.B,
        ["Greater Fire Penetration"] = (int)SupportTier.A,
        ["Lightning Penetration"] = (int)SupportTier.B,
        ["Greater Lightning Penetration"] = (int)SupportTier.A,
        ["Chaos Penetration"] = (int)SupportTier.B,
        ["Greater Chaos Penetration"] = (int)SupportTier.A,
        ["Hypothermia"] = (int)SupportTier.B,
        ["Greater Hypothermia"] = (int)SupportTier.A,
        ["Trap and Mine Damage"] = (int)SupportTier.B,
        ["Greater Trap and Mine Damage"] = (int)SupportTier.A,
        ["Multiple Traps"] = (int)SupportTier.B,
        ["Multiple Totems"] = (int)SupportTier.B,
        ["Throwing Speed"] = (int)SupportTier.B,
        ["Greater Throwing Speed"] = (int)SupportTier.A,
        ["Maim"] = (int)SupportTier.B,
        ["Impale Chance"] = (int)SupportTier.B,
        ["Greater Impale Chance"] = (int)SupportTier.A,
        ["Chance to Bleed"] = (int)SupportTier.B,
        ["Greater Chance to Bleed"] = (int)SupportTier.A,
        ["DoT Multiplier"] = (int)SupportTier.B,
        ["Greater DoT Multiplier"] = (int)SupportTier.A,
        ["Swift Affliction"] = (int)SupportTier.B,
        ["Greater Swift Affliction"] = (int)SupportTier.A,
        ["Wither on Hit"] = (int)SupportTier.B,
        ["Greater Wither on Hit"] = (int)SupportTier.A,
        ["Mirage Archer"] = (int)SupportTier.B,
        ["Ailment Damage"] = (int)SupportTier.B,
        ["Greater Ailment Damage"] = (int)SupportTier.A,
        ["Minion Damage"] = (int)SupportTier.B,
        ["Greater Minion Damage"] = (int)SupportTier.A,
        ["Minion Life"] = (int)SupportTier.B,
        ["Greater Minion Life"] = (int)SupportTier.A,
        ["Minion Caustic Death"] = (int)SupportTier.B,
        ["Generosity"] = (int)SupportTier.B,
        ["Greater Generosity"] = (int)SupportTier.A,
        ["Second Wind"] = (int)SupportTier.B,
        ["Fortify"] = (int)SupportTier.B,
        ["Greater Fortify"] = (int)SupportTier.A,
        ["More Duration"] = (int)SupportTier.B,
        ["Greater More Duration"] = (int)SupportTier.A,
        ["Warcry Speed"] = (int)SupportTier.B,
        ["Greater Warcry Speed"] = (int)SupportTier.A,
        ["Raging Cry"] = (int)SupportTier.B,
        ["Greater Raging Cry"] = (int)SupportTier.A,
        ["Infused Channelling"] = (int)SupportTier.B,
        ["Greater Infused Channelling"] = (int)SupportTier.A,
        ["Deadly Ailments"] = (int)SupportTier.D,
        ["Knockback"] = (int)SupportTier.C,
        ["Less Duration"] = (int)SupportTier.C,
        ["Ironwood"] = (int)SupportTier.C,
        ["Greater Ironwood"] = (int)SupportTier.C,
        ["Ailment Effect"] = (int)SupportTier.C,
        ["Greater Ailment Effect"] = (int)SupportTier.C,
        ["Brittle Chance"] = (int)SupportTier.C,
        ["Shock Chance"] = (int)SupportTier.C,
        ["Greater Shock Chance"] = (int)SupportTier.C,
        ["Freeze Chance"] = (int)SupportTier.C,
        ["Greater Freeze Chance"] = (int)SupportTier.C,
        ["Ignite Chance"] = (int)SupportTier.C,
        ["Greater Ignite Chance"] = (int)SupportTier.C,
        ["Chance to Poison"] = (int)SupportTier.C,
        ["Greater Chance to Poison"] = (int)SupportTier.C,
        ["Rage on Hit"] = (int)SupportTier.C,
        ["Greater Rage on Hit"] = (int)SupportTier.C,
    };

    internal static readonly Dictionary<string, int> DefaultSupportSkillOverrides = new()
    {
        ["Kinetic Blast of Clustering|Return"] = (int)SupportTier.S,
        ["Kinetic Blast of Clustering|Multiple Projectiles"] = (int)SupportTier.A,
        ["Kinetic Blast of Clustering|Greater Multiple Projectiles"] = (int)SupportTier.S,
        ["Ice Shot|Return"] = (int)SupportTier.S,
        ["Ice Shot|Elemental Damage with Attacks"] = (int)SupportTier.A,
        ["Vaal Ice Shot|Return"] = (int)SupportTier.S,
        ["Vaal Ice Shot|Cooldown Recovery"] = (int)SupportTier.A,
        ["Spectral Helix of Trarthus|Multiple Traps"] = (int)SupportTier.S,
        ["Spectral Helix of Trarthus|Trap and Mine Damage"] = (int)SupportTier.A,
        ["Spectral Helix of Trarthus|Slower Projectiles"] = (int)SupportTier.B,
        ["Static Strike|Elemental Damage with Attacks"] = (int)SupportTier.A,
        ["Static Strike|More Duration"] = (int)SupportTier.A,
        ["Static Strike|Chain"] = (int)SupportTier.B,
        ["Frost Blades|Return"] = (int)SupportTier.S,
        ["Frost Blades|Elemental Damage with Attacks"] = (int)SupportTier.A,
        ["Frost Blades|Chain"] = (int)SupportTier.B,
        ["Frost Blades|Hypothermia"] = (int)SupportTier.A,
        ["Frost Blades|Cold Penetration"] = (int)SupportTier.B,
        ["Vigilant Strike|Gilded Fortification"] = (int)SupportTier.S,
        ["Volcanic Fissure of Snaking|Gilded Additional Fissures"] = (int)SupportTier.A,
        ["Kinetic Blast of Clustering|Brutality"] = (int)SupportTier.D,
        ["Soulrend of Reaping|Brutality"] = (int)SupportTier.D,
        ["Wither|Brutality"] = (int)SupportTier.D,
        ["Void Sphere|Brutality"] = (int)SupportTier.D,
    };

    public void LoadAutoAssignedStrategy()
    {
        Settings.SkillFilter.Clear();
        Settings.SkillFilter.AddRange(DefaultGoodSkills);

        Settings.BadSkillFilter.Clear();
        Settings.BadSkillFilter.AddRange(DefaultBadSkills);

        ResetAllTiers();

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

    public void ClearAllStrategyData()
    {
        Settings.SkillFilter.Clear();
        Settings.BadSkillFilter.Clear();

        ResetAllTiers();

        Settings.SupportRatings.Clear();
        Settings.SupportSkillOverrides.Clear();

        LogMessage("Defaults restored: skill lists, archetype tiers and support ratings were zeroed.");
    }

    private void ResetAllTiers()
    {
        foreach (var key in MercenaryStats.Keys)
            Settings.MercenaryTiers[key] = (int)MercTier.None;
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
