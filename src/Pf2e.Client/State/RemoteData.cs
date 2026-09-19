namespace Pf2e.Client.State;

/// <summary>
/// The four states a remote fetch can be in. Three booleans would admit eight combinations of
/// which only these four are legal, and "loading and failed at once" is the bug that leaves a
/// spinner sitting over a stale list. The private constructor closes the set: no fifth case can
/// be declared outside this file.
/// </summary>
public abstract record RemoteData<T>
{
    private RemoteData() { }

    public sealed record NotAsked : RemoteData<T>;

    public sealed record Loading : RemoteData<T>;

    public sealed record Loaded(T Value) : RemoteData<T>;

    public sealed record Failed(string Message) : RemoteData<T>;
}
