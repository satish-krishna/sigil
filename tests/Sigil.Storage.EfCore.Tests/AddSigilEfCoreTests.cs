using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Sigil.Core.Storage;
using Sigil.Storage.EfCore;
using Sigil.Storage.EfCore.Tests.Infrastructure;
using Xunit;

namespace Sigil.Storage.EfCore.Tests;

[Collection("SigilDb")]
public class AddSigilEfCoreTests
{
    private readonly PostgresFixture _pg;
    public AddSigilEfCoreTests(PostgresFixture pg) => _pg = pg;

    private IConfiguration BuildConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:EfCore:ConnectionString"] = _pg.ConnectionString
            })
            .Build();

    private static IServiceCollection NewServices()
        => new ServiceCollection()
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    [Fact]
    public void Resolves_ISigilStore_As_EfSigilStore()
    {
        using var provider = NewServices()
            .AddSigilEfCore(BuildConfig())
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var resolved = scope.ServiceProvider.GetRequiredService<ISigilStore>();
        resolved.ShouldBeOfType<EfSigilStore>();
    }

    [Fact]
    public void Resolves_AllFiveSubStores_AsScoped()
    {
        using var provider = NewServices()
            .AddSigilEfCore(BuildConfig())
            .BuildServiceProvider();

        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;
        sp.GetRequiredService<IAgentRegistrationStore>().ShouldBeOfType<EfAgentRegistrationStore>();
        sp.GetRequiredService<IJobStore>().ShouldBeOfType<EfJobStore>();
        sp.GetRequiredService<IContextStore>().ShouldBeOfType<EfContextStore>();
        sp.GetRequiredService<ICheckpointStore>().ShouldBeOfType<EfCheckpointStore>();
        sp.GetRequiredService<IAuditStore>().ShouldBeOfType<EfAuditStore>();
    }

    [Fact]
    public void Options_BindFromConfiguration()
    {
        using var provider = NewServices()
            .AddSigilEfCore(BuildConfig())
            .BuildServiceProvider();

        var opts = provider.GetRequiredService<IOptions<SigilEfCoreOptions>>().Value;
        opts.ConnectionString.ShouldBe(_pg.ConnectionString);
    }
}
