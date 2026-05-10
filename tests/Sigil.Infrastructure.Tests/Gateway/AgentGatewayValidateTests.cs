using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayValidateTests
{
    private const string Key = "dev-key-echo";
    private const string AgentIdValue = "echo-agent";
    private const string EndpointUrl = "http://echo-agent:8080";

    private static ValidationRequest SampleRequest() => new()
    {
        Task = new AgentTask
        {
            JobId = new JobId("job-1"),
            StepId = new StepId("step-1"),
            SkillName = "echo"
        },
        AvailableTokenBudget = 1000
    };

    [Fact]
    public async Task HappyPath_Returns_DeserializedValidationResult()
    {
        var handler = new FakeHttpMessageHandler();
        var responseBody = JsonSerializer.Serialize(new
        {
            canHandle = true,
            estimatedTokens = 650,
            missingTools = Array.Empty<string>(),
            reason = (string?)null
        });
        handler.EnqueueResponse(HttpStatusCode.OK, responseBody);

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsSuccess.ShouldBeTrue();
        result.Value.CanHandle.ShouldBeTrue();
        result.Value.EstimatedTokens.ShouldBe(650);
    }

    [Fact]
    public async Task HappyPath_Posts_To_SigilValidate_With_SigilKeyHeader()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        handler.Requests.Count.ShouldBe(1);
        var request = handler.Requests[0];
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsoluteUri.ShouldBe($"{EndpointUrl}/sigil/validate");
        request.Headers.GetValues("X-Sigil-Key").ShouldHaveSingleItem().ShouldBe(Key);
        request.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task HappyPath_Sends_Request_Body_As_JsonValidationRequest()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        var sentJson = await handler.Requests[0].Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(sentJson);
        var root = doc.RootElement;
        root.GetProperty("task").GetProperty("jobId").GetString().ShouldBe("job-1");
        root.GetProperty("task").GetProperty("stepId").GetString().ShouldBe("step-1");
        root.GetProperty("task").GetProperty("skillName").GetString().ShouldBe("echo");
        root.GetProperty("availableTokenBudget").GetInt32().ShouldBe(1000);
    }

    [Fact]
    public async Task UnparseableResponseBody_Returns_ProtocolError()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, "not valid json {");

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(Sigil.Core.Gateway.SigilGatewayErrors.ProtocolError);
    }
}
