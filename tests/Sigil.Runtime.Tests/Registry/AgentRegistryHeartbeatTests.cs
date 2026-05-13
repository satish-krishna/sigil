using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.Registry;

public class AgentRegistryHeartbeatTests
{
    private static (AgentRegistry registry, FakeAgentRegistrationStore store) Make()
    {
        var store = new FakeAgentRegistrationStore();
        return (new AgentRegistry(store, new StubRandomProvider(seed: 1)), store);
    }

    [Theory]
    [InlineData(AgentStatus.Starting,  AgentStatus.Healthy)]
    [InlineData(AgentStatus.Healthy,   AgentStatus.Healthy)]
    [InlineData(AgentStatus.Degraded,  AgentStatus.Healthy)]
    [InlineData(AgentStatus.Draining,  AgentStatus.Draining)]
    public async Task Heartbeat_promotes_or_preserves_status(AgentStatus from, AgentStatus expected)
    {
        var (registry, store) = Make();
        await store.RegisterAsync(TestAgents.Make("alpha", status: from));
        var before = store.Snapshot[new AgentId("alpha")].LastHeartbeat;
        await Task.Delay(5);

        var result = await registry.HeartbeatAsync(new AgentId("alpha"));

        result.IsSuccess.ShouldBeTrue();
        var after = store.Snapshot[new AgentId("alpha")];
        after.Status.ShouldBe(expected);
        after.LastHeartbeat.ShouldBeGreaterThan(before);
    }

    [Fact]
    public async Task Heartbeat_rejects_offline_agent()
    {
        var (registry, store) = Make();
        await store.RegisterAsync(TestAgents.Make("alpha", status: AgentStatus.Offline));

        var result = await registry.HeartbeatAsync(new AgentId("alpha"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RegistryErrors.InvalidStatusTransition);
    }

    [Fact]
    public async Task Heartbeat_returns_agent_not_found_for_unknown_id()
    {
        var (registry, _) = Make();

        var result = await registry.HeartbeatAsync(new AgentId("ghost"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RegistryErrors.AgentNotFound);
    }
}
