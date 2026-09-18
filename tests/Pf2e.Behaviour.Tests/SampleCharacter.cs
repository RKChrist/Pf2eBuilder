using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

/// <summary>Reconciles the engine against a real exported level 7 Goblin Bard.</summary>
public class SampleCharacter
{
    private const int Level = 7;

    // The phase 0 brief states these inputs and expects 90, but 6 + 7 * (8 + 2 + 0) is 76.
    // Reaching 90 needs +12 per level, which is Con 18 (+4) with class HP 8. The export must be
    // re-read before this expectation is trusted either way.
    [Fact]
    public void SampleCharacterMaxHitPointsFromStatedInputsIs76()
    {
        var maxHp = HitPoints.Max(
            ancestryHp: 6,
            classHp: 8,
            level: Level,
            conModifier: Ability.Modifier(14),
            bonusHp: 0,
            bonusHpPerLevel: 0);

        Assert.Equal(76, maxHp);
    }

    [Fact]
    public void SampleCharacterArmorClassIs25()
    {
        // A potency rune raises the armour's own item bonus, so +1 studded leather is one +3 item
        // bonus, not a +2 and a +1 that the stacking rule would then refuse to combine.
        var armor = new Modifier("+1 studded leather", ModifierType.Item, 3, [ArmorClass.Target]);

        var ac = ArmorClass.Compute(
            level: Level,
            armorRank: ProficiencyRank.Trained,
            dexModifier: Ability.Modifier(16),
            armorDexCap: 3,
            modifiers: [armor]);

        Assert.Equal(25, ac.Total);
        Assert.Equal(["+1 studded leather"], ac.ContributingSources);
    }
}
