using System.Diagnostics;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using Pf2e.Api.Configuration;

namespace Pf2e.Api.Hosting;

/// <summary>
/// The client, started by the API in Development so that running one project runs the app.
/// <para>The two are separate deployables and that is the architecture, not an accident: the
/// client is static files that talk to this over HTTP, and in production one process serves both
/// (see <see cref="HostingOptions.ClientRoot"/>). None of that helps somebody who has just
/// pressed run on the API and is looking at a page that will not load, so in Development this
/// starts the other half too.</para>
/// <para>It refuses to do so when something already answers on the client's address, because the
/// commonest way to meet this is with a client already running in another window, and a second
/// one would fight it for the port and lose in a way that reads like the app being broken.</para>
/// </summary>
public sealed class ClientProcess(
    IOptions<HostingOptions> options, IHostEnvironment environment, ILogger<ClientProcess> log)
    : IHostedService
{
    Process? _client;

    public async Task StartAsync(CancellationToken ct)
    {
        var hosting = options.Value;
        var project = Path.GetFullPath(Path.Combine(environment.ContentRootPath, hosting.ClientProject));

        if (!Directory.Exists(project))
        {
            log.LogWarning(
                "{Setting} points at {Project}, which is not there, so the client was not started.",
                $"{HostingOptions.Section}:{nameof(HostingOptions.ClientProject)}", project);
            return;
        }

        if (await AnsweringAsync(hosting.ClientUrl, ct))
        {
            log.LogInformation("The client is already running at {Url}.", hosting.ClientUrl);
            return;
        }

        try
        {
            _client = Process.Start(new ProcessStartInfo("dotnet")
            {
                ArgumentList = { "run", "--project", project, "--launch-profile", "http" },
                WorkingDirectory = project,
                UseShellExecute = false,
            });

            // Tied to this process, so a stop button or a crash takes it too. Without that a
            // client outlives the API, the next run finds the port taken and leaves it alone,
            // and the page serves whatever the older build put there.
            var bound = _client is not null && ChildLifetime.Bind(_client);

            log.LogInformation(
                bound
                    ? "Started the client at {Url}. It takes a few seconds to build, and it stops when this does."
                    : "Started the client at {Url}. It takes a few seconds to build. Stop it with stop.cmd.",
                hosting.ClientUrl);
        }
        catch (Exception failure) when (failure is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Not being able to start the other half is not a reason this half cannot run.
            log.LogWarning(failure, "Could not start the client. Run it yourself with run.cmd.");
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        if (_client is { HasExited: false } running)
        {
            // The whole tree: "dotnet run" is a launcher, and killing it alone leaves the client
            // it started holding the port, which is exactly the orphan this class must not make.
            try
            {
                running.Kill(entireProcessTree: true);
            }
            catch (Exception failure) when (failure is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                log.LogWarning(failure, "Could not stop the client this process started.");
            }
        }

        _client?.Dispose();
        _client = null;
        return Task.CompletedTask;
    }

    /// <summary>A plain connection rather than a request, because the client answers nothing
    /// useful until it has finished building and this only needs to know whether the port is
    /// somebody else's.</summary>
    static async Task<bool> AnsweringAsync(string url, CancellationToken ct)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address))
        {
            return false;
        }

        try
        {
            using var probe = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(400));

            await probe.ConnectAsync(address.Host, address.Port, timeout.Token);
            return true;
        }
        catch (Exception failure) when (failure is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
