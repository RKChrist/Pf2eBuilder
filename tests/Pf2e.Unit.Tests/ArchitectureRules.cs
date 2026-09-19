using System.Reflection;
using Pf2e.Domain;

namespace Pf2e.Unit.Tests;

/// <summary>
/// Layering that is only a folder convention stops being true the first time someone is in a
/// hurry. These read the compiled assemblies, so a stray reference fails the build's tests
/// rather than being noticed in review.
/// </summary>
public class ArchitectureRules
{
    static readonly Assembly Domain = typeof(Selector).Assembly;

    static IEnumerable<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!);

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("MediatR")]
    [InlineData("FluentValidation")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.Extensions.DependencyInjection")]
    public void TheRulesEngineDependsOnNoFramework(string forbidden)
    {
        Assert.DoesNotContain(ReferencesOf(Domain), name =>
            name.StartsWith(forbidden, StringComparison.Ordinal));
    }

    [Fact]
    public void TheRulesEngineDependsOnNoOtherProjectOfOurs()
    {
        Assert.DoesNotContain(ReferencesOf(Domain), name =>
            name.StartsWith("Pf2e.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheContractsSharedWithTheClientCarryNoBehaviour()
    {
        var contracts = typeof(Pf2e.Contracts.Rules.RuleSummary).Assembly;

        Assert.DoesNotContain(ReferencesOf(contracts), name =>
            name.StartsWith("Pf2e.", StringComparison.Ordinal)
            || name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || name.StartsWith("MediatR", StringComparison.Ordinal));
    }

    [Fact]
    public void TheApplicationLayerNeverReachesForTheConcreteDbContext()
    {
        var application = typeof(Pf2e.Application.DependencyInjection).Assembly;

        Assert.DoesNotContain(ReferencesOf(application), name =>
            name.Equals("Pf2e.Infrastructure", StringComparison.Ordinal));
    }
}
