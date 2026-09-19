namespace Pf2e.Domain;

public enum SelectorKind
{
    Exactly,
    Governed,
    AllChecksAndDcs,
    SavingThrows,
    Speeds,
}

/// <summary>
/// A selector named in data rather than as an object, so it can be stored, sent over the wire,
/// and built from two dropdowns. <see cref="ToSelector"/> is the only way back.
/// </summary>
public readonly record struct SelectorSpec(
    SelectorKind Kind,
    StatKind Stat = default,
    AttributeKind Attribute = default,
    string? SkillName = null)
{
    public Selector ToSelector() => Kind switch
    {
        SelectorKind.Exactly => Selector.Exactly(Stat, SkillName),
        SelectorKind.Governed => Selector.Governed(Attribute),
        SelectorKind.AllChecksAndDcs => Selector.AllChecksAndDcs,
        SelectorKind.SavingThrows => Selector.SavingThrows,
        SelectorKind.Speeds => Selector.Speeds,
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Not a selector kind."),
    };

    public string Describe() => ToSelector().Describe();
}
