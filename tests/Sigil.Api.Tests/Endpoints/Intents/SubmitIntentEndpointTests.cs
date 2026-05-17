using System.Net;
using System.Net.Http.Json;
using CSharpFunctionalExtensions;
using Shouldly;
using Sigil.Api.Tests.Infrastructure;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Api.Tests.Endpoints.Intents;

public sealed class SubmitIntentEndpointTests
{
    private sealed record IntentDto(string SkillName, string Input);

    private static AgentRegistration NewHealthy(string id, string skill) => new()
    {
        AgentId = new AgentId(id),
        Name = id,
        Domain = "test",
        EndpointUrl = "https://localhost:9000",
        Status = AgentStatus.Healthy,
        Model = new ModelSpec { Provider = "test", Model = "test" },
        Skills = new[] { new Skill { Name = skill, Description = skill } },
    };

    [Fact]
    public async Task NoAgentForSkill_Returns404()
    {
        using var factory = new SigilApiFactory();
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsJsonAsync("/api/intents", new IntentDto("echo", "hi"));

        res.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"no-agent-for-skill\"");
    }

    [Fact]
    public async Task HappyPath_Returns200WithExecutionResult()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewHealthy(TestKeys.AgentB, "echo"));
        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);

        var res = await client.PostAsJsonAsync("/api/intents", new IntentDto("echo", "hi"));

        res.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AgentExecutionResult>();
        body.ShouldNotBeNull();
    }

    [Fact]
    public async Task ValidationRejected_Returns400()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewHealthy(TestKeys.AgentB, "echo"));
        factory.Gateway.OnValidate = (_, _) =>
            Result.Success(new ValidationResult { CanHandle = false, Reason = "tokens-exceeded" });

        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.PostAsJsonAsync("/api/intents", new IntentDto("echo", "hi"));

        res.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).ShouldContain("\"tokens-exceeded\"");
    }

    [Fact]
    public async Task GatewayFails_Returns502()
    {
        using var factory = new SigilApiFactory();
        await factory.Store.RegisterAsync(NewHealthy(TestKeys.AgentB, "echo"));
        factory.Gateway.OnValidate = (_, _) => Result.Failure<ValidationResult>(SigilGatewayErrors.CircuitOpen);

        var client = factory.CreateAuthedClient(TestKeys.AgentA, TestKeys.AgentAKey);
        var res = await client.PostAsJsonAsync("/api/intents", new IntentDto("echo", "hi"));

        res.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        (await res.Content.ReadAsStringAsync()).ShouldContain(SigilGatewayErrors.CircuitOpen);
    }
}
