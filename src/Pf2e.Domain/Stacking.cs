using System.Collections.Immutable;

namespace Pf2e.Domain;

public static class Stacking
{
    /// <summary>
    /// Applies the PF2e stacking rule to the modifiers that target <paramref name="target"/>:
    /// the highest bonus and the worst penalty of each typed kind (circumstance, item, status)
    /// apply, and every untyped modifier applies. The design brief's formula listed untyped
    /// penalties but was silent on untyped bonuses; they are summed the same way.
    /// </summary>
    public static Breakdown Resolve(int baseValue, StatTarget target, IEnumerable<Modifier> modifiers)
    {
        var applied = ImmutableArray.CreateBuilder<Modifier>();
        var suppressed = ImmutableArray.CreateBuilder<SuppressedModifier>();

        var relevant = modifiers.Where(m => m.AppliesTo(target) && m.Value != 0).ToList();

        foreach (var type in new[] { ModifierType.Circumstance, ModifierType.Item, ModifierType.Status })
        {
            KeepOnlyWinner(relevant.Where(m => m.Type == type && m.IsBonus), "bonus");
            KeepOnlyWinner(relevant.Where(m => m.Type == type && m.IsPenalty), "penalty");
        }

        applied.AddRange(relevant.Where(m => m.Type == ModifierType.Untyped));

        var total = baseValue + applied.Sum(m => m.Value);
        return new Breakdown(baseValue, applied.ToImmutable(), suppressed.ToImmutable(), total);

        void KeepOnlyWinner(IEnumerable<Modifier> sameTyped, string kind)
        {
            var ordered = sameTyped.OrderByDescending(m => Math.Abs(m.Value)).ToList();
            if (ordered.Count == 0)
            {
                return;
            }

            var winner = ordered[0];
            applied.Add(winner);
            foreach (var loser in ordered.Skip(1))
            {
                suppressed.Add(new SuppressedModifier(
                    loser,
                    $"{winner.Type.ToString().ToLowerInvariant()} {kind} does not stack; {winner.Source} ({winner.Value:+#;-#}) is larger"));
            }
        }
    }
}
