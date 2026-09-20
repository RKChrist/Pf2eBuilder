using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

/// <summary>
/// What a screen means when it marks a number as changed.
/// <para>A card says Armor Class 25 whether 25 is what the character has or what a buff made of
/// 22, and the only way to tell used to be opening each number in turn. The mark exists to
/// answer that at a glance, so it has to mean "somebody did this", not "you are wearing
/// armour": a mark that is always on says nothing.</para>
/// </summary>
public class SwingRules
{
    static readonly StatTarget Armor = StatTarget.ArmorClass;

    static Modifier Worn(string source, ModifierType type, int value) =>
        new(source, type, value, [Selector.Exactly(StatKind.ArmorClass)], ModifierOrigin.Gear);

    static Modifier Cast(string source, ModifierType type, int value) =>
        new(source, type, value, [Selector.Exactly(StatKind.ArmorClass)]);

    [Fact]
    public void ANumberNothingIsTouchingHasNoSwing()
    {
        var result = Stacking.Resolve(20, Armor, []);

        Assert.Equal(20, result.Total);
        Assert.Equal(0, result.Swing);
    }

    // The case that made the first version of this useless: every character is wearing armour,
    // so every armour class was marked as changed and the mark meant nothing.
    [Fact]
    public void WornArmourIsNotSomethingSomebodyDid()
    {
        var result = Stacking.Resolve(20, Armor, [Worn("Studded Leather", ModifierType.Item, 3)]);

        Assert.Equal(23, result.Total);
        Assert.Equal(0, result.Swing);
    }

    [Fact]
    public void ARaisedShieldIs()
    {
        var result = Stacking.Resolve(20, Armor,
        [
            Worn("Studded Leather", ModifierType.Item, 3),
            Cast("Raise a Shield", ModifierType.Circumstance, 2),
        ]);

        Assert.Equal(25, result.Total);
        Assert.Equal(2, result.Swing);
    }

    [Fact]
    public void AndSoIsAConditionTheOtherWay()
    {
        var result = Stacking.Resolve(20, Armor,
        [
            Worn("Studded Leather", ModifierType.Item, 3),
            Cast("Frightened 2", ModifierType.Status, -2),
        ]);

        Assert.Equal(21, result.Total);
        Assert.Equal(-2, result.Swing);
    }

    // The reason the swing is a second resolve rather than a sum. A +2 item bonus from a spell
    // suppresses the armour's +1 and is worth one point, not two: taking the spell away gives
    // the armour's bonus back. Summing the applied effect modifiers would say two.
    [Fact]
    public void AnEffectThatSuppressesWornGearIsWorthOnlyTheDifference()
    {
        var result = Stacking.Resolve(20, Armor,
        [
            Worn("Leather", ModifierType.Item, 1),
            Cast("Mage Armor", ModifierType.Item, 2),
        ]);

        Assert.Equal(22, result.Total);
        Assert.Equal(1, result.Swing);

        // And the breakdown still shows the whole story: the worn bonus is suppressed, not gone.
        var held = Assert.Single(result.Suppressed);
        Assert.Equal("Leather", held.Modifier.Source);
    }

    // The mirror: an effect the worn gear beats changes nothing at all, and a screen must not
    // mark the number as though something had happened.
    [Fact]
    public void AnEffectTheGearBeatsMovesNothing()
    {
        var result = Stacking.Resolve(20, Armor,
        [
            Worn("Full Plate", ModifierType.Item, 6),
            Cast("Mage Armor", ModifierType.Item, 1),
        ]);

        Assert.Equal(26, result.Total);
        Assert.Equal(0, result.Swing);
    }
}
