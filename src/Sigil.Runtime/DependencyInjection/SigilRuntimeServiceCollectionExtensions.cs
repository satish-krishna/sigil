using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;

namespace Sigil.Runtime.DependencyInjection;

public static class SigilRuntimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers runtime services: <see cref="IAgentRegistry"/> and its dependencies.
    /// Requires <c>IAgentRegistrationStore</c> to be registered separately by the chosen storage provider.
    /// </summary>
    public static IServiceCollection AddSigilRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<IRandomProvider, SystemRandomProvider>();
        services.AddScoped<IAgentRegistry, AgentRegistry>();
        return services;
    }
}
