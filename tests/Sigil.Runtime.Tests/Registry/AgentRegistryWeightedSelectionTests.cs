using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Sigil.Runtime.Registry;
using Xunit;

namespace Sigil.Runtime.Tests.Registry;

public class AgentRegistryWeightedSelectionTests
{
    private static AgentRegistry Make(FakeAgentRegistrationStore store, IRandomProvider random)
        => new(store, random);

    [Fact]
    public async Task Empty_skill_name_throws_ArgumentException()
    {
        var store = new FakeAgentRegistrationStore();
        var registry = Make(store, new StubRandomProvider(seed: 1));

        // SkillNameRequired surfaces as ArgumentException for invalid input.
        await Should.ThrowAsync<ArgumentException>(
            () => registry.SelectByWeightAsync("  "));
    }

    [Fact]
    public async Task No_agents_for_skill_returns_None()
    {
        var store = new FakeAgentRegistrationStore();
        var registry = Make(store, new StubRandomProvider(seed: 1));

        var pick = await registry.SelectByWeightAsync("echo");

        pick.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task All_candidates_unhealthy_returns_None()
    {
        var store = new FakeAgentRegistrationStore();
        await store.RegisterAsync(TestAgents.Make("a", status: AgentStatus.Degraded));
        await store.RegisterAsync(TestAgents.Make("b", status: AgentStatus.Offline));
        await store.RegisterAsync(TestAgents.Make("c", status: AgentStatus.Draining));
        await store.RegisterAsync(TestAgents.Make("d", status: AgentStatus.Starting));
        var registry = Make(store, new StubRandomProvider(seed: 1));

        var pick = await registry.SelectByWeightAsync("echo");

        pick.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task Zero_weight_candidates_are_excluded()
    {
        var store = new FakeAgentRegistrationStore();
        await store.RegisterAsync(TestAgents.Make("zero", status: AgentStatus.Healthy, routingWeight: 0));
        var registry = Make(store, new StubRandomProvider(seed: 1));

        var pick = await registry.SelectByWeightAsync("echo");

        pick.HasValue.ShouldBeFalse();
    }

    [Fact]
    public async Task Single_healthy_candidate_is_always_selected()
    {
        var store = new FakeAgentRegistrationStore();
        await store.RegisterAsync(TestAgents.Make("solo", status: AgentStatus.Healthy, routingWeight: 5));
        var registry = Make(store, new StubRandomProvider(seed: 1));

        var pick = await registry.SelectByWeightAsync("echo");

        pick.HasValue.ShouldBeTrue();
        pick.Value.AgentId.Value.ShouldBe("solo");
    }

    [Fact]
    public async Task Weighted_distribution_matches_weights_within_tolerance()
    {
        var store = new FakeAgentRegistrationStore();
        // Deterministic order by AgentId: "canary" < "stable"
        await store.RegisterAsync(TestAgents.Make("canary", status: AgentStatus.Healthy, routingWeight: 10));
        await store.RegisterAsync(TestAgents.Make("stable", status: AgentStatus.Healthy, routingWeight: 90));

        var registry = Make(store, new StubRandomProvider(seed: 42));

        const int draws = 10_000;
        var counts = new Dictionary<string, int> { ["canary"] = 0, ["stable"] = 0 };
        for (var i = 0; i < draws; i++)
        {
            var pick = await registry.SelectByWeightAsync("echo");
            pick.HasValue.ShouldBeTrue();
            counts[pick.Value.AgentId.Value]++;
        }

        var canaryRatio = counts["canary"] / (double)draws;
        canaryRatio.ShouldBeInRange(0.08, 0.12); // 10% ± 2 pp
    }

    [Fact]
    public async Task Deterministic_pick_uses_running_total_against_roll()
    {
        // Order: "a" (w=10), "b" (w=20), "c" (w=70). Total = 100.
        // Queued rolls: 5 (< 10 → a), 25 (10 ≤ 25 < 30 → b), 95 (30 ≤ 95 < 100 → c)
        var store = new FakeAgentRegistrationStore();
        await store.RegisterAsync(TestAgents.Make("a", status: AgentStatus.Healthy, routingWeight: 10));
        await store.RegisterAsync(TestAgents.Make("b", status: AgentStatus.Healthy, routingWeight: 20));
        await store.RegisterAsync(TestAgents.Make("c", status: AgentStatus.Healthy, routingWeight: 70));
        var registry = Make(store, new StubRandomProvider(new[] { 5, 25, 95 }));

        (await registry.SelectByWeightAsync("echo")).Value.AgentId.Value.ShouldBe("a");
        (await registry.SelectByWeightAsync("echo")).Value.AgentId.Value.ShouldBe("b");
        (await registry.SelectByWeightAsync("echo")).Value.AgentId.Value.ShouldBe("c");
    }
}
