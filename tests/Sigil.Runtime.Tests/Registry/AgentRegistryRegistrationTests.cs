using CSharpFunctionalExtensions;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.Registry;

public class AgentRegistryRegistrationTests
{
    private static AgentRegistry NewRegistry(out FakeAgentRegistrationStore store)
    {
        store = new FakeAgentRegistrationStore();
        return new AgentRegistry(store, new StubRandomProvider(seed: 1));
    }

    [Fact]
    public async Task Register_persists_agent_with_status_Starting()
    {
        var registry = NewRegistry(out var store);
        var agent = TestAgents.Make("alpha", status: AgentStatus.Healthy /* should be overridden */);

        var result = await registry.RegisterAsync(agent);

        result.IsSuccess.ShouldBeTrue();
        store.Snapshot[new AgentId("alpha")].Status.ShouldBe(AgentStatus.Starting);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Register_rejects_invalid_routing_weight(int weight)
    {
        var registry = NewRegistry(out var store);
        var agent = TestAgents.Make("alpha", routingWeight: weight);

        var result = await registry.RegisterAsync(agent);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RegistryErrors.InvalidRoutingWeight);
        store.Snapshot.ShouldBeEmpty();
    }

    [Fact]
    public async Task Register_overwrites_offline_agent_back_to_Starting()
    {
        var registry = NewRegistry(out var store);
        await store.RegisterAsync(TestAgents.Make("alpha", status: AgentStatus.Offline));

        var result = await registry.RegisterAsync(TestAgents.Make("alpha"));

        result.IsSuccess.ShouldBeTrue();
        store.Snapshot[new AgentId("alpha")].Status.ShouldBe(AgentStatus.Starting);
    }
}
