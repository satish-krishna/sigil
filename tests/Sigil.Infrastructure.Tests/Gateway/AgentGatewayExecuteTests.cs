using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayExecuteTests
{
    private const string Key = "dev-key-echo";
    private const string AgentIdValue = "echo-agent";
    private const string EndpointUrl = "http://echo-agent:8080";

    private static AgentExecutionPackage SamplePackage() => new()
    {
        Task = new AgentTask
        {
            JobId = new JobId("job-1"),
            StepId = new StepId("step-1"),
            SkillName = "echo"
        },
        Snapshot = new ContextSnapshot { JobId = new JobId("job-1") },
        ExpectedETag = new ETag("etag-1")
    };

    [Fact]
    public async Task HappyPath_Returns_DeserializedExecutionResult()
    {
        var handler = new FakeHttpMessageHandler();
        var responseBody = JsonSerializer.Serialize(new
        {
            delta = new { updates = new Dictionary<string, object>(), removals = Array.Empty<string>() },
            logs = Array.Empty<object>(),
            metrics = new { promptTokens = 100, completionTokens = 200, duration = "00:00:01" }
        });
        handler.EnqueueResponse(HttpStatusCode.OK, responseBody);

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ExecuteAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SamplePackage());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Metrics.PromptTokens.ShouldBe(100);
    }

    [Fact]
    public async Task HappyPath_Posts_To_SigilExecute()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            delta = new { updates = new Dictionary<string, object>(), removals = Array.Empty<string>() },
            logs = Array.Empty<object>(),
            metrics = new { promptTokens = 0, completionTokens = 0, duration = "00:00:00" }
        }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ExecuteAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SamplePackage());

        handler.Requests[0].RequestUri!.AbsoluteUri.ShouldBe($"{EndpointUrl}/sigil/execute");
        handler.Requests[0].Headers.GetValues("X-Sigil-Key").ShouldHaveSingleItem().ShouldBe(Key);
    }

    [Fact]
    public async Task HappyPath_Sends_Request_Body_As_JsonExecutionPackage()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            delta = new { updates = new Dictionary<string, object>(), removals = Array.Empty<string>() },
            logs = Array.Empty<object>(),
            metrics = new { promptTokens = 0, completionTokens = 0, duration = "00:00:00" }
        }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        await gateway.ExecuteAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SamplePackage());

        var sentJson = await handler.Requests[0].Content!.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(sentJson);
        var root = doc.RootElement;
        root.GetProperty("task").GetProperty("jobId").GetString().ShouldBe("job-1");
        root.GetProperty("snapshot").GetProperty("jobId").GetString().ShouldBe("job-1");
        root.GetProperty("expectedETag").GetString().ShouldBe("etag-1");
    }
}
