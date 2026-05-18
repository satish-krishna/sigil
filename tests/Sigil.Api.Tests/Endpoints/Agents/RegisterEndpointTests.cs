using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Api.Tests.Endpoints.Agents;

public sealed class RegisterEndpointTests
{
    private static AgentRegistration NewRegistration(string id = TestKeys.AgentA) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = "test",
        EndpointUrl = "https://localhost:9000",
        Model = new ModelSpec { Provider = "test", Model = "test" },
        Skills = new[] { new Skill { Name = "echo", Description = "echo back" } },
    };

    [Fact]
    public async Task HappyPath_Returns201()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsJsonAsync("/api/agents/register", NewRegistration());

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<AgentRegistration>();
        body!.AgentId.Value.ShouldBe(TestKeys.AgentA);
    }

    [Fact]
    public async Task CallerMismatch_Returns403()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsJsonAsync("/api/agents/register", NewRegistration(TestKeys.AgentB));

        res.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"caller-agent-mismatch\"");
    }

    [Fact]
    public async Task DuplicateAgent_Returns409()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        await client.PostAsJsonAsync("/api/agents/register", NewRegistration());

        var res = await client.PostAsJsonAsync("/api/agents/register", NewRegistration());

        res.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"duplicate-agent\"");
    }

    [Fact]
    public async Task ClientSuppliedTimestamps_AreOverwritten()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var spoofed = NewRegistration() with
        {
            RegisteredAt = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastHeartbeat = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = AgentStatus.Healthy,
        };
        var before = DateTime.UtcNow;

        var res = await client.PostAsJsonAsync("/api/agents/register", spoofed);

        res.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<AgentRegistration>();
        body!.Status.ShouldBe(AgentStatus.Starting);
        body.RegisteredAt.ShouldBeGreaterThanOrEqualTo(before);
        body.LastHeartbeat.ShouldBeGreaterThanOrEqualTo(before);
    }

    [Fact]
    public async Task InvalidRoutingWeight_Returns400()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var bad = NewRegistration() with { RoutingWeight = 999 };

        var res = await client.PostAsJsonAsync("/api/agents/register", bad);

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"invalid-routing-weight\"");
    }
}
