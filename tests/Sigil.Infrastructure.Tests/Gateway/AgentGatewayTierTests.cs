using Shouldly;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayTierTests
{
    [Theory]
    [InlineData(SecurityTier.Standard)]
    [InlineData(SecurityTier.Trusted)]
    public async Task NonOpenTier_Fails_With_TierNotSupported_AndMakesNoHttpCall(SecurityTier tier)
    {
        var handler = new FakeHttpMessageHandler();
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey("echo-agent", "dev-key-echo"));

        var agent = GatewayTestHarness.MakeRegistration(tier: tier);
        var request = new ValidationRequest
        {
            Task = new AgentTask
            {
                JobId = new JobId("job-1"),
                StepId = new StepId("step-1"),
                SkillName = "echo"
            },
            AvailableTokenBudget = 1000
        };

        var result = await gateway.ValidateAsync(agent, request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.TierNotSupported);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(SecurityTier.Standard)]
    [InlineData(SecurityTier.Trusted)]
    public async Task ExecuteAsync_NonOpenTier_Fails_With_TierNotSupported_AndMakesNoHttpCall(SecurityTier tier)
    {
        var handler = new FakeHttpMessageHandler();
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey("echo-agent", "dev-key-echo"));

        var agent = GatewayTestHarness.MakeRegistration(tier: tier);
        var package = new AgentExecutionPackage
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

        var result = await gateway.ExecuteAsync(agent, package);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.TierNotSupported);
        handler.Requests.ShouldBeEmpty();
    }
}
