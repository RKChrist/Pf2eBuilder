using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Pf2e.Contracts.Accounts;

namespace Pf2e.Client.Api;

public sealed class AccountApiException(string message) : Exception(message);

/// <summary>
/// Signing in, and asking who this browser is.
/// <para>Every call here carries its credentials. The session is an encrypted cookie the browser
/// cannot read, which is the whole point of design/012, and a fetch sends a cookie to another
/// origin only when it is told to. The client is a separate deployable from the API, so "another
/// origin" is the ordinary case rather than the exotic one.</para>
/// </summary>
public sealed class AccountsApi(HttpClient http)
{
    public Task<AccountView> RegisterAsync(RegisterAccountRequest request, CancellationToken ct) =>
        SendAsync<AccountView>(Carrying(HttpMethod.Post, "accounts", request), ct);

    public Task<AccountView> SignInAsync(SignInRequest request, CancellationToken ct) =>
        SendAsync<AccountView>(Carrying(HttpMethod.Post, "accounts/session", request), ct);

    public async Task SignOutAsync(CancellationToken ct)
    {
        using var request = Credentialled(new HttpRequestMessage(HttpMethod.Delete, "accounts/session"));
        using var response = await SendOrThrowAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new AccountApiException(await ReadFailureAsync(response, ct));
        }
    }

    /// <summary>
    /// Null for a browser nobody is signed in on, which is an answer rather than a refusal: the
    /// route says so with a 200 and an empty session. This screen is asked on every page load,
    /// and a 401 would print a console error on every one of them.
    /// </summary>
    public async Task<AccountView?> WhoAmIAsync(CancellationToken ct) =>
        (await SendAsync<SessionView>(
            Credentialled(new HttpRequestMessage(HttpMethod.Get, "accounts/session")), ct)).Account;

    static HttpRequestMessage Carrying<T>(HttpMethod method, string url, T body) =>
        Credentialled(new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) });

    static HttpRequestMessage Credentialled(HttpRequestMessage request)
    {
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        return request;
    }

    async Task<T> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        {
            using var response = await SendOrThrowAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new AccountApiException(await ReadFailureAsync(response, ct));
            }

            return await ReadAsync<T>(response, ct);
        }
    }

    async Task<HttpResponseMessage> SendOrThrowAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, ct);
        }
        catch (HttpRequestException)
        {
            throw new AccountApiException("The service is not reachable.");
        }
    }

    static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(ct)
                   ?? throw new AccountApiException("The service sent an empty answer.");
        }
        catch (JsonException)
        {
            throw new AccountApiException("The service sent an answer this app could not read.");
        }
    }

    /// <summary>The sentence the server wrote, when it wrote one. A refusal a person is meant to
    /// act on, such as an email that is already taken, says so in its body.</summary>
    static async Task<string> ReadFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemTitle>(ct);
            if (problem?.Title is { Length: > 0 } title)
            {
                return title;
            }
        }
        catch (Exception failure) when (failure is JsonException or NotSupportedException)
        {
        }

        return $"The service refused that ({(int)response.StatusCode}).";
    }

    sealed record ProblemTitle(string? Title);
}
