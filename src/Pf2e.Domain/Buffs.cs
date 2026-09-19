using System.Collections.Immutable;

namespace Pf2e.Domain;

/// <summary>
/// The bonuses a table actually applies to itself: a raised shield, cover, the bard's anthems,
/// a bless, a heroism.
/// <para>These are not seeded. Archives of Nethys publishes the name and the page of every one
/// of them and the structured effect of none of them, and this project does not import the
/// rule text. So the numbers here were written out by hand against the printed rule, which is
/// why every entry carries <see cref="EffectDefinition.Verified"/> false and names its page.
/// Check them before you trust them at a table.</para>
/// <para>Three buffs a table uses are deliberately absent. Aid and Guidance grant a bonus to
/// one check the player names, and this engine can only say "every check", which would inflate
/// every number on the sheet instead of the one that was aided. Haste grants an action and no
/// modifier at all. Add those as custom effects until the engine can hold a bonus that waits
/// for the roll it belongs to.</para>
/// </summary>
public static class Buffs
{
    static EffectDefinition Fixed(
        string key, string name, ModifierType type, int value, string sourceRef, string category,
        params Selector[] applies) =>
        new(key, name, HasValue: false, [new ModifierTemplate.Bonus(type, value, [.. applies])],
            sourceRef, Verified: false, EffectKind.Buff, category);

    static EffectDefinition Valued(
        string key, string name, ModifierType type, string sourceRef, string category,
        params Selector[] applies) =>
        new(key, name, HasValue: true, [new ModifierTemplate.ScalingBonus(type, [.. applies])],
            sourceRef, Verified: false, EffectKind.Buff, category);

    static EffectDefinition Named(EffectDefinition effect, string ruleName) =>
        effect with { RuleName = ruleName };

    /// <summary>
    /// The bonus is the shield's own, which is why this one carries a value: a buckler is 1, most
    /// shields are 2, and a tower shield behind which you have also Taken Cover is 4. Storing 2 for
    /// everyone would quietly overstate a buckler and understate a tower shield.
    /// </summary>
    public static EffectDefinition RaiseAShield { get; } =
        Valued("raise-a-shield", "Raise a Shield", ModifierType.Circumstance,
            "Player Core pg. 419", "action",
            Selector.Exactly(StatKind.ArmorClass));

    /// <summary>
    /// Lesser cover is 1, standard cover 2, greater cover 4, so the value is the kind of cover.
    /// The printed rule also gives the same bonus to Reflex saves against area effects and to
    /// Stealth checks to Hide and Sneak. Neither is here: this engine has no way to say "against
    /// an area effect" or "only when Hiding", and applying it to every Reflex save and every
    /// Stealth check would be a larger error than leaving it out.
    /// </summary>
    public static EffectDefinition Cover { get; } =
        Named(Valued("cover", "Cover", ModifierType.Circumstance,
            "Player Core pg. 432", "action",
            Selector.Exactly(StatKind.ArmorClass)), "Take Cover");

    public static EffectDefinition Shield { get; } =
        Fixed("shield-spell", "Shield", ModifierType.Circumstance, 1,
            "Player Core pg. 349", "spell",
            Selector.Exactly(StatKind.ArmorClass));

    public static EffectDefinition Bless { get; } =
        Fixed("bless", "Bless", ModifierType.Status, 1,
            "Player Core pg. 322", "spell",
            Selector.AttackRolls);

    /// <summary>
    /// The printed rule also covers saves against fear, which needs a predicate on the trait of
    /// the thing being saved against. The engine has no such predicate, so a frightened bard's
    /// party gets the attack and damage halves here and the save half nowhere.
    /// </summary>
    public static EffectDefinition CourageousAnthem { get; } =
        Fixed("courageous-anthem", "Courageous Anthem", ModifierType.Status, 1,
            "Player Core pg. 330", "spell",
            Selector.AttackRolls, Selector.Exactly(StatKind.Damage));

    /// <summary>
    /// The printed rule also grants resistance to physical damage. Resistance is not a modifier
    /// to a statistic and this engine holds only modifiers, so it is not here.
    /// </summary>
    public static EffectDefinition RallyingAnthem { get; } =
        Fixed("rallying-anthem", "Rallying Anthem", ModifierType.Status, 1,
            "Player Core pg. 341", "spell",
            Selector.Exactly(StatKind.ArmorClass), Selector.SavingThrows);

    /// <summary>Cast at rank 3 the bonus is 1, at rank 6 it is 2, at rank 9 it is 3, so the value
    /// is the bonus and the caster sets it from the rank they spent.</summary>
    public static EffectDefinition Heroism { get; } =
        Valued("heroism", "Heroism", ModifierType.Status,
            "Player Core pg. 337", "spell",
            Selector.AttackRolls, Selector.Exactly(StatKind.Perception),
            Selector.SavingThrows, Selector.Exactly(StatKind.Skill));

    public static ImmutableArray<EffectDefinition> All { get; } =
    [
        RaiseAShield, Cover, Shield, Bless, CourageousAnthem, RallyingAnthem, Heroism,
    ];
}

/// <summary>Every effect the engine can apply by key, whether it reads as a condition or a buff.
/// One list, because the stacking rule does not care which a modifier came from.</summary>
public static class Effects
{
    public static ImmutableArray<EffectDefinition> All { get; } = [.. Conditions.All, .. Buffs.All];

    public static EffectDefinition? Find(string key) =>
        All.FirstOrDefault(e => string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));
}
