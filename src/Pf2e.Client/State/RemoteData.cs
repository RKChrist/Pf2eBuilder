namespace Pf2e.Client.State;

/// <summary>The private constructor closes the set: no fifth case can be declared elsewhere.</summary>
public abstract record RemoteData<T>
{
    private RemoteData() { }

    public sealed record NotAsked : RemoteData<T>;

    public sealed record Loading : RemoteData<T>;

    public sealed record Loaded(T Value) : RemoteData<T>;

    public sealed record Failed(string Message) : RemoteData<T>;
}
