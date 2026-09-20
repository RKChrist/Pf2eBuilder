namespace Pf2e.Application.Abstractions;

/// <summary>
/// Where a rule's own words come from.
/// <para>The seed holds mechanics and deliberately no prose: withheld text is never downloaded by
/// the importer, and the tracked snapshot stays that way. A description is asked for one record at
/// a time, when somebody opens that record, from the site the record already links to.</para>
/// </summary>
public interface IRuleTextSource
{
    Task<RuleTextAnswer> FetchAsync(string ruleId, CancellationToken ct);
}

/// <summary>Closed, because "the site has no words for this" and "the site could not be reached"
/// are different sentences on screen and only one of them is worth trying again.</summary>
public abstract record RuleTextAnswer
{
    private RuleTextAnswer() { }

    /// <summary>The record's page as the site writes it, header and all.</summary>
    public sealed record Found(string Markdown) : RuleTextAnswer;

    public sealed record None : RuleTextAnswer;

    public sealed record Unreachable : RuleTextAnswer;
}
