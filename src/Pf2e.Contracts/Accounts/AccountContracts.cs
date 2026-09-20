namespace Pf2e.Contracts.Accounts;

/// <summary>Everything about an account that anybody is ever sent. The hash is not in it and
/// neither is anything else the store holds.</summary>
public sealed record AccountView(Guid Id, string Email, string DisplayName);

/// <summary>
/// The answer to "who is this browser", which has a legitimate answer of nobody. A null Account
/// is that answer and not a refusal, because nothing was denied: a signed-out visitor asking is
/// the ordinary case and a 4xx would make every page load log an error.
/// </summary>
public sealed record SessionView(AccountView? Account);

public sealed record RegisterAccountRequest(string Email, string DisplayName, string Password);

public sealed record SignInRequest(string Email, string Password);
