namespace Pf2e.Domain.Tracking;

/// <summary>
/// One effect on one character, in the shape a row holds it.
/// <see cref="Id"/> is the key rather than the character and the effect's name, because the
/// same effect can be applied twice and the stacking rule must be the thing that suppresses
/// one, visibly. It is also the slot the client names, so resending an apply converges instead
/// of stacking a duplicate.
/// </summary>
public sealed class TrackedEffect
{
    public required Guid Id { get; init; }
    public required Guid CharacterId { get; init; }

    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
    public string? Duration { get; set; }
    public string SourceKind { get; set; } = string.Empty;
    public string? SourceKey { get; set; }
    public List<EffectModifier> Modifiers { get; set; } = [];

    public ActiveEffect ToActive() =>
        new(Id, Name, Value, EffectSource.From(SourceKind, SourceKey, [.. Modifiers]), Duration);

    public static TrackedEffect From(Guid characterId, ActiveEffect active)
    {
        var effect = new TrackedEffect { Id = active.Id, CharacterId = characterId };
        effect.Overwrite(active);
        return effect;
    }

    /// <summary>The update half of the upsert, beside the insert half, so a slot that already
    /// holds an effect and one that does not end up with the same columns.</summary>
    public void Overwrite(ActiveEffect active)
    {
        Name = active.Name;
        Value = active.Value;
        Duration = active.Duration;
        SourceKind = active.Source.Kind;
        SourceKey = active.Source.StoredKey;
        Modifiers = [.. active.Source.StoredModifiers];
    }
}
