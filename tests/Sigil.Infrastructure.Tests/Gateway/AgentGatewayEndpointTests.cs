using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayEndpointTests
{
    private const string Key = "dev-key-echo";
    private const string AgentIdValue = "echo-agent";

    private static ValidationRequest SampleRequest() => new()
    {
        Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
        AvailableTokenBudget = 1000
    };

    [Theory]
    [InlineData("http://echo-agent:8080", "http://echo-agent:8080/sigil/validate")]
    [InlineData("http://echo-agent:8080/", "http://echo-agent:8080/sigil/validate")]
    [InlineData("http://echo-agent:8080/v1", "http://echo-agent:8080/v1/sigil/validate")]
    [InlineData("http://echo-agent:8080/v1/", "http://echo-agent:8080/v1/sigil/validate")]
    [InlineData("https://agent.example.com/api/v2", "https://agent.example.com/api/v2/sigil/validate")]
    public async Task EndpointUrl_Composes_With_SubPath(string endpointUrl, string expectedAbsoluteUri)
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, endpointUrl),
            SampleRequest());

        handler.Requests[0].RequestUri!.AbsoluteUri.ShouldBe(expectedAbsoluteUri);
    }
}
