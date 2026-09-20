using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pf2e.Application.Abstractions;
using Pf2e.Infrastructure.Configuration;
using Pf2e.Infrastructure.Persistence;
using Pf2e.Infrastructure.Rules;
using Pf2e.Infrastructure.Security;

namespace Pf2e.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddSection<PersistenceOptions>(config, PersistenceOptions.Section);
        services.AddSection<SeedingOptions>(config, SeedingOptions.Section);

        services.AddDbContext<RulesDbContext>((provider, db) =>
        {
            var persistence = provider.GetRequiredService<IOptions<PersistenceOptions>>().Value;
            switch (persistence.Provider)
            {
                case "Sqlite":
                    db.UseSqlite(persistence.ConnectionString);
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Database:Provider is '{persistence.Provider}', which this build does not support. " +
                        "Supported providers: Sqlite.");
            }
        });

        services.AddScoped<IRulesDbContext>(provider => provider.GetRequiredService<RulesDbContext>());
        services.AddScoped<ITrackerDbContext>(provider => provider.GetRequiredService<RulesDbContext>());
        services.AddScoped<IAccountsDbContext>(provider => provider.GetRequiredService<RulesDbContext>());
        services.AddSingleton<IUndoStack, MemoryUndoStack>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<RulesSeeder>();

        services.AddSection<RuleTextOptions>(config, RuleTextOptions.Section);
        services.AddHttpClient<IRuleTextSource, AonRuleTextSource>((provider, http) =>
        {
            var text = provider.GetRequiredService<IOptions<RuleTextOptions>>().Value;
            http.BaseAddress = new Uri(text.Endpoint);
            http.Timeout = TimeSpan.FromSeconds(text.TimeoutSeconds);
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Pf2eBuilder/1.0 (table companion; one record per open)");
        });
        return services;
    }
}
