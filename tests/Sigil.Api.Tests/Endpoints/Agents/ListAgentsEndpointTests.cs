using System.Net;
using System.Net.Http.Json;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Api.Tests.Endpoints.Agents;

public sealed class ListAgentsEndpointTests
{
    private static AgentRegistration NewAgent(string id, string domain, string skill) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = domain,
        EndpointUrl = "https://localhost:9000",
        Model = new ModelSpec { Provider = "test", Model = "test" },
        Skills = new[] { new Skill { Name = skill, Description = skill } },
    };

    [Fact]
    public async Task Empty_ReturnsEmptyArray()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AgentRegistration[]>();
        body.ShouldNotBeNull();
        body.Length.ShouldBe(0);
    }

    [Fact]
    public async Task Populated_ReturnsAll()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, "test", "echo"));
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentB, "test", "reverse"));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var body = await client.GetFromJsonAsync<AgentRegistration[]>("/api/agents");

        body!.Length.ShouldBe(2);
    }

    [Fact]
    public async Task FilterBySkill_ReturnsMatching()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, "test", "echo"));
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentB, "test", "reverse"));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var body = await client.GetFromJsonAsync<AgentRegistration[]>("/api/agents?skill=echo");

        body!.Length.ShouldBe(1);
        body[0].AgentId.Value.ShouldBe(TestKeys.AgentA);
    }

    [Fact]
    public async Task FilterByDomain_ReturnsMatching()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentA, "alpha", "echo"));
        await factory.Store.RegisterAsync(NewAgent(TestKeys.AgentB, "beta", "echo"));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var body = await client.GetFromJsonAsync<AgentRegistration[]>("/api/agents?domain=alpha");

        body!.Length.ShouldBe(1);
        body[0].AgentId.Value.ShouldBe(TestKeys.AgentA);
    }

    [Fact]
    public async Task BothFilters_Returns400()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.GetAsync("/api/agents?skill=echo&domain=alpha");

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"conflicting-filters\"");
    }
}
