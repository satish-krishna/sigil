using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Infrastructure.Gateway;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayActivityTests
{
    [Fact]
    public async Task ValidateAsync_Emits_Activity_With_StandardTags()
    {
        // Use a unique parent activity as a root so we can isolate our span by TraceId,
        // preventing cross-test pollution from other concurrently-running gateway tests
        // that share the global ActivityListener.
        using var root = new Activity("test.root").Start();
        var testTraceId = root.TraceId;

        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Sigil.Gateway",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.TraceId == testTraceId)
                    stopped.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);

        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(HttpStatusCode.OK,
            JsonSerializer.Serialize(new { canHandle = true, missingTools = Array.Empty<string>() }));

        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey("echo-agent", "dev-key-echo"));

        await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration("echo-agent", "http://echo-agent:8080"),
            new ValidationRequest
            {
                Task = new AgentTask
                {
                    JobId = new JobId("job-1"),
                    StepId = new StepId("step-1"),
                    SkillName = "echo"
                },
                AvailableTokenBudget = 1000
            });

        var activity = stopped.ShouldHaveSingleItem();
        activity.OperationName.ShouldBe("agent.validate");
        activity.GetTagItem("sigil.agent.id").ShouldBe("echo-agent");
        activity.GetTagItem("sigil.agent.endpoint").ShouldBe("http://echo-agent:8080");
        activity.GetTagItem("sigil.agent.tier").ShouldBe("Open");
        activity.GetTagItem("sigil.gateway.method").ShouldBe("validate");
        activity.GetTagItem("sigil.job.id").ShouldBe("job-1");
        activity.GetTagItem("sigil.step.id").ShouldBe("step-1");
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
    }
}
