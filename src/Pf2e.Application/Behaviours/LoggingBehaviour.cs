using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Pf2e.Application.Behaviours;

public sealed class LoggingBehaviour<TRequest, TResponse>(ILogger<LoggingBehaviour<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            return await next(ct);
        }
        finally
        {
            log.LogInformation("{Request} finished in {Elapsed}ms", typeof(TRequest).Name, timer.ElapsedMilliseconds);
        }
    }
}
