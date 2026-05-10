using System.Net;
using Shouldly;
using Sigil.Core.Gateway;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayOutcomeTests
{
    private const string Key = "dev-key-echo";
    private const string AgentIdValue = "echo-agent";
    private const string EndpointUrl = "http://echo-agent:8080";

    private static ValidationRequest SampleRequest() => new()
    {
        Task = new AgentTask
        {
            JobId = new JobId("j"), StepId = new StepId("s"), SkillName = "echo"
        },
        AvailableTokenBudget = 1000
    };

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, SigilGatewayErrors.AgentRejectedCredentials)]
    [InlineData(HttpStatusCode.Forbidden,    SigilGatewayErrors.AgentRejectedCredentials)]
    [InlineData(HttpStatusCode.NotFound,     SigilGatewayErrors.AgentNotFound)]
    [InlineData(HttpStatusCode.BadRequest,   SigilGatewayErrors.AgentRejected)]
    [InlineData(HttpStatusCode.Conflict,     SigilGatewayErrors.AgentRejected)]
    [InlineData(HttpStatusCode.InternalServerError, SigilGatewayErrors.AgentError)]
    [InlineData(HttpStatusCode.BadGateway,         SigilGatewayErrors.AgentError)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SigilGatewayErrors.AgentError)]
    public async Task HttpStatus_Maps_To_ExpectedErrorCode(HttpStatusCode status, string expectedCode)
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueResponse(status);
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(expectedCode);
        // No retries in the raw-client harness — the handler must have seen exactly one request.
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task HttpRequestException_Returns_TransportError()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueException(new HttpRequestException("connection refused"));
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest());

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.TransportError);
    }

    [Fact]
    public async Task CancelledToken_Returns_Cancelled()
    {
        var handler = new FakeHttpMessageHandler();
        // Don't enqueue a response — the cancellation should fire before SendAsync touches the queue.
        var gateway = GatewayTestHarness.WithRawClient(
            handler,
            security: GatewayTestHarness.OpenWithKey(AgentIdValue, Key));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await gateway.ValidateAsync(
            GatewayTestHarness.MakeRegistration(AgentIdValue, EndpointUrl),
            SampleRequest(),
            cts.Token);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(SigilGatewayErrors.Cancelled);
    }
}
