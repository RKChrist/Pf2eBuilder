namespace Pf2e.Domain.Tracking;

/// <summary>
/// A campaign comes into being when the first character is imported into it, which is why no
/// operation creates one. This type knows nothing about how it is stored.
/// </summary>
public sealed class Campaign
{
    public required Guid Id { get; init; }
    public required string Code { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public List<TrackedCharacter> Characters { get; init; } = [];
}
