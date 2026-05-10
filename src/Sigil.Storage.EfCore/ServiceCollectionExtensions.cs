using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sigil.Core.Storage;

namespace Sigil.Storage.EfCore;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSigilEfCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<SigilEfCoreOptions>()
            .Bind(configuration.GetSection(SigilEfCoreOptions.SectionName))
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.ConnectionString),
                $"{SigilEfCoreOptions.SectionName}:ConnectionString must be set.")
            .ValidateOnStart();

        services.AddDbContext<SigilDbContext>((sp, opts) =>
        {
            var settings = sp.GetRequiredService<IOptions<SigilEfCoreOptions>>().Value;
            opts.UseNpgsql(settings.ConnectionString);
        });

        services.AddScoped<IAgentRegistrationStore, EfAgentRegistrationStore>();
        services.AddScoped<IJobStore, EfJobStore>();
        services.AddScoped<IContextStore, EfContextStore>();
        services.AddScoped<ICheckpointStore, EfCheckpointStore>();
        services.AddScoped<IAuditStore, EfAuditStore>();

        services.AddScoped<ISigilStore>(sp => new EfSigilStore(
            sp.GetRequiredService<IAgentRegistrationStore>(),
            sp.GetRequiredService<IJobStore>(),
            sp.GetRequiredService<IContextStore>(),
            sp.GetRequiredService<ICheckpointStore>(),
            sp.GetRequiredService<IAuditStore>()));

        return services;
    }
}
