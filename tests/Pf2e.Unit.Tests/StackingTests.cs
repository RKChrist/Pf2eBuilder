using Pf2e.Domain;

namespace Pf2e.Unit.Tests;

public class StackingTests
{
    private static readonly StatTarget Will = new(StatKind.Will);

    private static Modifier Mod(string source, ModifierType type, int value) =>
        new(source, type, value, [Selector.Exactly(StatKind.Will)]);

    [Fact]
    public void NoModifiersLeavesBaseUnchanged()
    {
        var result = Stacking.Resolve(12, Will, []);

        Assert.Equal(12, result.Base);
        Assert.Equal(12, result.Total);
        Assert.Empty(result.Applied);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void ZeroValuedModifierIsNeitherAppliedNorSuppressed()
    {
        var result = Stacking.Resolve(12, Will, [Mod("Nothing", ModifierType.Status, 0)]);

        Assert.Empty(result.Applied);
        Assert.Empty(result.Suppressed);
    }

    [Fact]
    public void ThreeSameTypedBonusesKeepOnlyTheHighestAndSuppressBoth()
    {
        var result = Stacking.Resolve(0, Will,
        [
            Mod("A", ModifierType.Item, 1),
            Mod("B", ModifierType.Item, 3),
            Mod("C", ModifierType.Item, 2),
        ]);

        Assert.Equal(3, result.Total);
        Assert.Equal(["A", "C"], result.Suppressed.Select(s => s.Modifier.Source).Order());
    }

    [Fact]
    public void SuppressionReasonNamesTheWinner()
    {
        var result = Stacking.Resolve(0, Will,
        [
            Mod("Weaker", ModifierType.Circumstance, -1),
            Mod("Stronger", ModifierType.Circumstance, -2),
        ]);

        var reason = Assert.Single(result.Suppressed).Reason;
        Assert.Contains("Stronger", reason);
        Assert.Contains("circumstance penalty", reason);
    }

    [Theory]
    [InlineData(ModifierType.Circumstance)]
    [InlineData(ModifierType.Item)]
    [InlineData(ModifierType.Status)]
    public void EachTypedKindKeepsItsOwnBestBonusAndWorstPenalty(ModifierType type)
    {
        var result = Stacking.Resolve(0, Will,
        [
            Mod("Bonus 1", type, 1),
            Mod("Bonus 2", type, 2),
            Mod("Penalty 1", type, -1),
            Mod("Penalty 3", type, -3),
        ]);

        Assert.Equal(-1, result.Total);
        Assert.Equal(["Bonus 2", "Penalty 3"], result.ContributingSources.Order());
    }
}
