using System.Net;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Xunit;

namespace Sigil.Api.Tests.Security;

public sealed class SigilAuthMiddlewareTests : IClassFixture<SigilApiFactory>
{
    private readonly SigilApiFactory _factory;

    public SigilAuthMiddlewareTests(SigilApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MissingBothHeaders_Returns401_MissingCredentials()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"missing-credentials\"");
    }

    [Fact]
    public async Task MissingKeyOnly_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Sigil-Agent-Id", TestKeys.AgentA);
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"missing-credentials\"");
    }

    [Fact]
    public async Task UnknownAgent_Returns401()
    {
        var client = _factory.CreateAuthedClient("never-registered", "any-key");
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"unknown-agent\"");
    }

    [Fact]
    public async Task WrongKey_Returns401()
    {
        var client = _factory.CreateAuthedClient(TestKeys.AgentA, "wrong-key");
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"key-mismatch\"");
    }

    [Fact(Skip = "Endpoint registered in Task 15 — re-enable after.")]
    public async Task ValidHeaders_ReachesEndpoint()
    {
        var client = _factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.GetAsync("/api/agents");

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
