using System.Net;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Api.Tests.Endpoints.Agents;

public sealed class DeregisterEndpointTests
{
    private static AgentRegistration NewHealthy(string id) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = "test",
        EndpointUrl = "https://localhost:9000",
        Status = AgentStatus.Healthy,
        Model = new ModelSpec { Provider = "test", Model = "test" },
        Skills = new[] { new Skill { Name = "echo", Description = "echo" } },
    };

    [Fact]
    public async Task HappyPath_Returns204()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewHealthy(TestKeys.AgentA));
        await factory.Store.UpdateStatusAsync(new AgentId(TestKeys.AgentA), AgentStatus.Healthy);

        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/deregister", content: null);

        res.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task UnknownAgent_Returns404()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentA}/deregister", content: null);

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"agent-not-found\"");
    }

    [Fact]
    public async Task CallerMismatch_Returns403()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsync($"/api/agents/{TestKeys.AgentB}/deregister", content: null);

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"caller-agent-mismatch\"");
    }
}
