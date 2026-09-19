using Microsoft.Extensions.Options;

namespace Pf2e.Api.Infrastructure.Configuration;

public static class OptionsRegistration
{
    /// <summary>
    /// Binds a configuration section in the post-configure stage, so appsettings always wins.
    /// Every <c>Configure</c> delegate runs before every <c>PostConfigure</c> delegate, which
    /// means a library that sets its own default during <c>AddX()</c> cannot beat the operator.
    /// </summary>
    public static IServiceCollection AddSection<TOptions>(
        this IServiceCollection services, IConfiguration config, string section)
        where TOptions : class
    {
        services.AddOptions<TOptions>()
                .PostConfigure(options => config.GetSection(section).Bind(options))
                .ValidateDataAnnotations()
                .ValidateOnStart();
        return services;
    }
}
