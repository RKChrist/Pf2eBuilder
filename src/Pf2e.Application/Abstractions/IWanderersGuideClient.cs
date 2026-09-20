using Pf2e.Application.Features.Campaigns;

namespace Pf2e.Application.Abstractions;

/// <summary>One character its owner has shared, read from Wanderer's Guide at that owner's
/// request. Nothing else of theirs is read, and never in bulk.</summary>
public interface IWanderersGuideClient
{
    Task<WanderersGuideAnswer> FindCharacterAsync(WanderersGuideCharacterId id, CancellationToken ct);
}

/// <summary>Closed, because each arm is a different sentence to the player and only one of them
/// is fixed by trying again.</summary>
public abstract record WanderersGuideAnswer
{
    private WanderersGuideAnswer() { }

    /// <summary>The character object as their API returns it, which is the same object an
    /// export holds under <c>character</c>.</summary>
    public sealed record Found(string CharacterJson) : WanderersGuideAnswer;

    public sealed record NotShared : WanderersGuideAnswer;

    public sealed record NotFound : WanderersGuideAnswer;

    public sealed record Unreachable : WanderersGuideAnswer;
}
