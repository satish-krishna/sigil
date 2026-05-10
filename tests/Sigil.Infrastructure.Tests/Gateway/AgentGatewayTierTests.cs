using Shouldly;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Security;
using Sigil.Infrastructure.Security;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayTierTests
{
    [Theory]
    [InlineData(SecurityTier.Standard)]
    [InlineData(SecurityTier.Trusted)]
    public async Task NonOpen_KernelMode_Fails_With_TierNotSupported(SecurityTier kernelMode)
    {
        var handler = new FakeHttpMessageHandler();
        var security = new SigilSecurityOptions { Mode = kernelMode };
        security.OpenTier.Keys["echo-agent"] = "dev-key-echo";
        var gateway = GatewayTestHarness.WithRawClient(handler, security: security);

        var agent = GatewayTestHarness.MakeRegistration(); // agent claims Open tier
        var request = new ValidationRequest
        {
            Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
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

    [Fact]
    public async Task OpenTier_NoAllowlistEntry_Fails_With_OutboundKeyMissing()
    {
        var handler = new FakeHttpMessageHandler();
        // Allowlist intentionally empty: agent claims Open tier but has no key configured.
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: new SigilSecurityOptions { Mode = SecurityTier.Open });

        var agent = GatewayTestHarness.MakeRegistration(agentId: "echo-agent");
        var request = new ValidationRequest
        {
            Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
            AvailableTokenBudget = 1000
        };

        var result = await gateway.ValidateAsync(agent, request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.OutboundKeyMissing);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    [InlineData("/just/a/path")]
    [InlineData("ftp://example.com")]
    public async Task InvalidEndpoint_Fails_With_EndpointInvalid(string endpointUrl)
    {
        var handler = new FakeHttpMessageHandler();
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey("echo-agent", "dev-key-echo"));

        var agent = GatewayTestHarness.MakeRegistration(endpointUrl: endpointUrl);
        var request = new ValidationRequest
        {
            Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
            AvailableTokenBudget = 1000
        };

        var result = await gateway.ValidateAsync(agent, request);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.EndpointInvalid);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_NoAllowlistEntry_Fails_With_OutboundKeyMissing()
    {
        var handler = new FakeHttpMessageHandler();
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: new SigilSecurityOptions { Mode = SecurityTier.Open });

        var agent = GatewayTestHarness.MakeRegistration(agentId: "echo-agent");
        var package = new AgentExecutionPackage
        {
            Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
            Snapshot = new ContextSnapshot { JobId = new JobId("j") },
            ExpectedETag = new ETag("etag-1")
        };

        var result = await gateway.ExecuteAsync(agent, package);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.OutboundKeyMissing);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-uri")]
    [InlineData("/just/a/path")]
    [InlineData("ftp://example.com")]
    public async Task ExecuteAsync_InvalidEndpoint_Fails_With_EndpointInvalid(string endpointUrl)
    {
        var handler = new FakeHttpMessageHandler();
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey("echo-agent", "dev-key-echo"));

        var agent = GatewayTestHarness.MakeRegistration(endpointUrl: endpointUrl);
        var package = new AgentExecutionPackage
        {
            Task = new AgentTask { JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo" },
            Snapshot = new ContextSnapshot { JobId = new JobId("j") },
            ExpectedETag = new ETag("etag-1")
        };

        var result = await gateway.ExecuteAsync(agent, package);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.EndpointInvalid);
        handler.Requests.ShouldBeEmpty();
    }
}
