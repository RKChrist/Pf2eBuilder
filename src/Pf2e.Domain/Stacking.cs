using System.Collections.Immutable;

namespace Pf2e.Domain;

public static class Stacking
{
    /// <summary>
    /// Applies the PF2e stacking rule to the modifiers that target <paramref name="target"/>:
    /// the highest bonus and the worst penalty of each typed kind (circumstance, item, status)
    /// apply, and every untyped modifier applies. The design brief's formula listed untyped
    /// penalties but was silent on untyped bonuses; they are summed the same way.
    /// <para>The answer carries a second total, run over the worn modifiers alone. The
    /// difference between the two is what the effects on this creature are doing, which is not
    /// the sum of the applied effect modifiers: an anthem's status bonus can suppress a worn
    /// one, and taking the anthem off gives the worn bonus back rather than losing both. A
    /// screen that summed the list would say the anthem is worth more than it is.</para>
    /// </summary>
    public static Breakdown Resolve(int baseValue, StatTarget target, IEnumerable<Modifier> modifiers)
    {
        var relevant = modifiers.Where(m => m.AppliesTo(target) && m.Value != 0).ToList();
        var (applied, suppressed, total) = Run(baseValue, relevant);
        var worn = relevant.Where(m => m.Origin is ModifierOrigin.Gear).ToList();

        // Only worth a second pass when an effect is actually in play. Nothing on the creature
        // means the bare total is the total, and most creatures most of the time have nothing.
        var bare = worn.Count == relevant.Count ? total : Run(baseValue, worn).Total;

        return new Breakdown(baseValue, applied, suppressed, total, bare);
    }

    static (ImmutableArray<Modifier> Applied, ImmutableArray<SuppressedModifier> Suppressed, int Total)
        Run(int baseValue, List<Modifier> relevant)
    {
        var applied = ImmutableArray.CreateBuilder<Modifier>();
        var suppressed = ImmutableArray.CreateBuilder<SuppressedModifier>();

        foreach (var type in new[] { ModifierType.Circumstance, ModifierType.Item, ModifierType.Status })
        {
            KeepOnlyWinner(relevant.Where(m => m.Type == type && m.IsBonus), "bonus");
            KeepOnlyWinner(relevant.Where(m => m.Type == type && m.IsPenalty), "penalty");
        }

        applied.AddRange(relevant.Where(m => m.Type == ModifierType.Untyped));

        return (applied.ToImmutable(), suppressed.ToImmutable(), baseValue + applied.Sum(m => m.Value));

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
