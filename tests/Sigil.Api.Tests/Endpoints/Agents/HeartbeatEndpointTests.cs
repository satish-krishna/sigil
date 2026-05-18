using System.Net;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Api.Tests.Endpoints.Agents;

public sealed class HeartbeatEndpointTests
{
    private static AgentRegistration NewAgent(string id, AgentStatus status) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = "test",
        EndpointUrl = "https://localhost:9000",
        Status = status,
        Model = new ModelSpec { Provider = "test", Model = "test" },
        Skills = new[] { new Skill { Name = "echo", Description = "echo" } },
    };

    [Fact]
    public async Task HappyPath_Returns204()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, AgentStatus.Healthy));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/heartbeat", null);

        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnknownAgent_Returns404()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/heartbeat", null);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"agent-not-found\"");
    }

    [Fact]
    public async Task OfflineAgent_Returns409()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, AgentStatus.Offline));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/heartbeat", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"invalid-status-transition\"");
    }

    [Fact]
    public async Task CallerMismatch_Returns403()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentB}/heartbeat", null);

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
