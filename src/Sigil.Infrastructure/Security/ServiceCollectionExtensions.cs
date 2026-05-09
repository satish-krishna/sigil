using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sigil.Core.Security;

namespace Sigil.Infrastructure.Security;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSigilSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<SigilSecurityOptions>()
            .Bind(configuration.GetSection(SigilSecurityOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton<ISigilSecurity, SigilKeyValidator>();
        return services;
    }
}
