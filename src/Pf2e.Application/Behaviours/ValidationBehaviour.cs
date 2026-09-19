using FluentValidation;
using MediatR;

namespace Pf2e.Application.Behaviours;

/// <summary>
/// Runs every validator registered for a request before its handler. Validation is a
/// cross-cutting concern, so it lives here rather than as the first ten lines of each handler.
/// </summary>
public sealed class ValidationBehaviour<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!validators.Any())
        {
            return await next(ct);
        }

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(ct);
    }
}
