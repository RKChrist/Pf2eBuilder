namespace Pf2e.Domain.Rules;

/// <summary>
/// What a record used to be called, and which record it is now.
/// <para>Only genuine renames are here. Most of what the source marks superseded is a
/// renumbering that kept its name, and a direct name match already finds those; what is left is
/// Inspire Competence becoming Uplifting Overture and Dimension Door becoming Translocate.</para>
/// <para>This is the one thing a Pathbuilder export written before the Remaster needs. Without
/// it a character's feats show their old names and open nothing.</para>
/// </summary>
public sealed class RuleAlias
{
    /// <summary>The old name, which is what an export carries.</summary>
    public required string Was { get; init; }

    /// <summary>The category the old record was in, so a renamed feat cannot resolve to a spell.</summary>
    public required string Category { get; init; }

    /// <summary>The record it became, which is in <see cref="RuleRecord"/>.</summary>
    public required string NowId { get; init; }
}
