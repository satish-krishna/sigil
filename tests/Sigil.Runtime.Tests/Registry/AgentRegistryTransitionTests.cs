using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.Registry;

public class AgentRegistryTransitionTests
{
    private static (AgentRegistry registry, FakeAgentRegistrationStore store) Make()
    {
        var store = new FakeAgentRegistrationStore();
        return (new AgentRegistry(store, new StubRandomProvider(seed: 1)), store);
    }

    public static IEnumerable<object[]> LegalTransitions => new[]
    {
        new object[] { AgentStatus.Starting,  AgentStatus.Healthy },
        new object[] { AgentStatus.Starting,  AgentStatus.Offline },
        new object[] { AgentStatus.Healthy,   AgentStatus.Degraded },
        new object[] { AgentStatus.Healthy,   AgentStatus.Offline },
        new object[] { AgentStatus.Healthy,   AgentStatus.Draining },
        new object[] { AgentStatus.Degraded,  AgentStatus.Healthy },
        new object[] { AgentStatus.Degraded,  AgentStatus.Offline },
        new object[] { AgentStatus.Degraded,  AgentStatus.Draining },
        new object[] { AgentStatus.Draining,  AgentStatus.Offline },
    };

    public static IEnumerable<object[]> IllegalTransitions => new[]
    {
        new object[] { AgentStatus.Starting,  AgentStatus.Degraded },
        new object[] { AgentStatus.Starting,  AgentStatus.Draining },
        new object[] { AgentStatus.Healthy,   AgentStatus.Healthy },
        new object[] { AgentStatus.Degraded,  AgentStatus.Degraded },
        new object[] { AgentStatus.Offline,   AgentStatus.Healthy },
        new object[] { AgentStatus.Offline,   AgentStatus.Degraded },
        new object[] { AgentStatus.Offline,   AgentStatus.Draining },
        new object[] { AgentStatus.Draining,  AgentStatus.Healthy },
        new object[] { AgentStatus.Draining,  AgentStatus.Degraded },
        new object[] { AgentStatus.Draining,  AgentStatus.Draining },
    };

    [Theory]
    [MemberData(nameof(LegalTransitions))]
    public async Task Legal_transition_succeeds(AgentStatus from, AgentStatus to)
    {
        var (registry, store) = Make();
        await store.RegisterAsync(TestAgents.Make("alpha", status: from));

        var result = await InvokeTransition(registry, new AgentId("alpha"), to);

        result.IsSuccess.ShouldBeTrue($"{from} → {to} should be legal");
        store.Snapshot[new AgentId("alpha")].Status.ShouldBe(to);
    }

    [Theory]
    [MemberData(nameof(IllegalTransitions))]
    public async Task Illegal_transition_is_rejected(AgentStatus from, AgentStatus to)
    {
        var (registry, store) = Make();
        await store.RegisterAsync(TestAgents.Make("alpha", status: from));

        var result = await InvokeTransition(registry, new AgentId("alpha"), to);

        result.IsFailure.ShouldBeTrue($"{from} → {to} should be illegal");
        result.Error.ShouldBe(RegistryErrors.InvalidStatusTransition);
        store.Snapshot[new AgentId("alpha")].Status.ShouldBe(from);
    }

    [Fact]
    public async Task Transition_returns_agent_not_found_for_unknown_id()
    {
        var (registry, _) = Make();

        var result = await registry.MarkHealthyAsync(new AgentId("ghost"));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(RegistryErrors.AgentNotFound);
    }

    private static Task<CSharpFunctionalExtensions.Result> InvokeTransition(
        AgentRegistry registry, AgentId id, AgentStatus to) => to switch
    {
        AgentStatus.Healthy   => registry.MarkHealthyAsync(id),
        AgentStatus.Degraded  => registry.MarkDegradedAsync(id),
        AgentStatus.Offline   => registry.MarkOfflineAsync(id),
        AgentStatus.Draining  => registry.BeginDrainingAsync(id),
        _ => throw new ArgumentOutOfRangeException(nameof(to), to, "Test does not cover this target.")
    };
}
