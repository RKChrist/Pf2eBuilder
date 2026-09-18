using Pf2e.Domain;

namespace Pf2e.Behaviour.Tests;

public class StackingRules
{
    private static readonly StatTarget Attack = new(StatKind.Attack);

    private static Modifier Mod(string source, ModifierType type, int value) =>
        new(source, type, value, [Attack]);

    [Fact]
    public void SameTypeBonusesDoNotStack_HighestWins()
    {
        var result = Stacking.Resolve(0, Attack,
        [
            Mod("Bless", ModifierType.Status, 1),
            Mod("Heroism", ModifierType.Status, 2),
        ]);

        Assert.Equal(2, result.Total);
        Assert.Equal(["Heroism"], result.ContributingSources);
        var loser = Assert.Single(result.Suppressed);
        Assert.Equal("Bless", loser.Modifier.Source);
    }

    [Fact]
    public void SameTypePenaltiesDoNotStack_WorstWins()
    {
        var result = Stacking.Resolve(0, Attack,
        [
            Mod("Frightened 1", ModifierType.Status, -1),
            Mod("Sickened 3", ModifierType.Status, -3),
        ]);

        Assert.Equal(-3, result.Total);
        Assert.Equal(["Sickened 3"], result.ContributingSources);
    }

    [Fact]
    public void StatusBonusAndStatusPenaltyBothApply()
    {
        var result = Stacking.Resolve(0, Attack,
        [
            Mod("Courageous Anthem", ModifierType.Status, 1),
            Mod("Sickened 2", ModifierType.Status, -2),
        ]);

        Assert.Equal(-1, result.Total);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void UntypedPenaltiesAlwaysStack()
    {
        var result = Stacking.Resolve(0, Attack,
        [
            Mod("Multiple attack penalty", ModifierType.Untyped, -5),
            Mod("Agile second attack", ModifierType.Untyped, -4),
        ]);

        Assert.Equal(-9, result.Total);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void UntypedBonusesAlwaysStack()
    {
        var result = Stacking.Resolve(0, Attack,
        [
            Mod("Untyped A", ModifierType.Untyped, 1),
            Mod("Untyped B", ModifierType.Untyped, 2),
        ]);

        Assert.Equal(3, result.Total);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void DifferentTypedBonusesStackWithEachOther()
    {
        var result = Stacking.Resolve(0, Attack,
        [
            Mod("Aid", ModifierType.Circumstance, 1),
            Mod("Potency rune", ModifierType.Item, 1),
            Mod("Bless", ModifierType.Status, 1),
        ]);

        Assert.Equal(3, result.Total);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void BreakdownNamesEverySourceThatContributed()
    {
        var result = Stacking.Resolve(7, Attack,
        [
            Mod("Aid", ModifierType.Circumstance, 2),
            Mod("Potency rune", ModifierType.Item, 1),
            Mod("Bless", ModifierType.Status, 1),
            Mod("Heroism", ModifierType.Status, 2),
            Mod("Multiple attack penalty", ModifierType.Untyped, -5),
        ]);

        Assert.Equal(7, result.Base);
        Assert.Equal(7 + 2 + 1 + 2 - 5, result.Total);
        Assert.Equal(
            ["Aid", "Heroism", "Multiple attack penalty", "Potency rune"],
            result.ContributingSources.Order());

        var suppressed = Assert.Single(result.Suppressed);
        Assert.Equal("Bless", suppressed.Modifier.Source);
        Assert.Contains("Heroism", suppressed.Reason);
    }

    [Fact]
    public void ModifierTargetingAnotherStatisticIsIgnored()
    {
        var acOnly = new Modifier("Shield raised", ModifierType.Circumstance, 2, [new(StatKind.ArmorClass)]);

        var result = Stacking.Resolve(0, Attack, [acOnly]);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Applied);
        Assert.Empty(result.Suppressed);
    }
}
