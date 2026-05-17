using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Sigil.Core.Registry;
using Sigil.Core.Storage;
using Sigil.Runtime.DependencyInjection;
using Sigil.Runtime.Registry;
using Sigil.Runtime.Tests.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.DependencyInjection;

public class AddSigilRuntimeTests
{
    private static IServiceCollection NewServices()
        => new ServiceCollection().AddSingleton<IAgentRegistrationStore, FakeAgentRegistrationStore>();

    [Fact]
    public void Registers_IAgentRegistry_as_AgentRegistry()
    {
        using var provider = NewServices().AddSigilRuntime().BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IAgentRegistry>();

        resolved.ShouldBeOfType<AgentRegistry>();
    }

    [Fact]
    public void Registers_default_IRandomProvider_as_SystemRandomProvider()
    {
        using var provider = NewServices().AddSigilRuntime().BuildServiceProvider();

        var resolved = provider.GetRequiredService<IRandomProvider>();

        resolved.ShouldBeOfType<SystemRandomProvider>();
    }

    [Fact]
    public void IRandomProvider_is_singleton_across_repeated_AddSigilRuntime_calls()
    {
        var services = NewServices().AddSigilRuntime().AddSigilRuntime();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IRandomProvider>();
        var second = provider.GetRequiredService<IRandomProvider>();

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void Pre_registered_IRandomProvider_wins_over_default()
    {
        var custom = new StubRandomProvider(seed: 7);
        var services = NewServices();
        services.AddSingleton<IRandomProvider>(custom);
        services.AddSigilRuntime();

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IRandomProvider>();

        resolved.ShouldBeSameAs(custom);
    }

    [Fact]
    public void Resolving_IAgentRegistry_without_store_throws()
    {
        var services = new ServiceCollection().AddSigilRuntime();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Should.Throw<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<IAgentRegistry>());
    }
}
