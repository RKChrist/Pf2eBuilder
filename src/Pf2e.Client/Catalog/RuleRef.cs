namespace Pf2e.Client.Catalog;

/// <summary>
/// What a screen knows about the rule behind a name it is showing.
/// <para>Most of the screen has an id, because the thing being shown came out of the ruleset and
/// carried its record with it. The rest has only the words a rules text uses, such as the
/// condition a domain table names or the activity a chooser lists, and nothing in the app knows
/// which record those words belong to. The id behind them is looked up when somebody actually
/// asks, not while a list is being drawn.</para>
/// <para>The private constructor closes the set: a name is one or the other and never a third
/// thing.</para>
/// </summary>
public abstract record RuleRef
{
    private RuleRef() { }

    public sealed record Id(string RuleId) : RuleRef;

    public sealed record Named(string Category, string Name) : RuleRef;
}
