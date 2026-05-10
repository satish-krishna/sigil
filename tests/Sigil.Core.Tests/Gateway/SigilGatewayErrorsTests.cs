using Shouldly;
using Sigil.Core.Gateway;
using Xunit;

namespace Sigil.Core.Tests.Gateway;

public class SigilGatewayErrorsTests
{
    [Fact]
    public void ErrorCodes_HaveStableValues()
    {
        // Pre-flight
        SigilGatewayErrors.TierNotSupported.ShouldBe("tier-not-supported");
        SigilGatewayErrors.OutboundKeyMissing.ShouldBe("outbound-key-missing");
        SigilGatewayErrors.EndpointInvalid.ShouldBe("endpoint-invalid");

        // HTTP outcomes
        SigilGatewayErrors.AgentRejectedCredentials.ShouldBe("agent-rejected-credentials");
        SigilGatewayErrors.AgentNotFound.ShouldBe("agent-not-found");
        SigilGatewayErrors.AgentRejected.ShouldBe("agent-rejected");
        SigilGatewayErrors.AgentError.ShouldBe("agent-error");

        // Transport / resilience
        SigilGatewayErrors.Timeout.ShouldBe("timeout");
        SigilGatewayErrors.CircuitOpen.ShouldBe("circuit-open");
        SigilGatewayErrors.TransportError.ShouldBe("transport-error");

        // Protocol / cancellation
        SigilGatewayErrors.ProtocolError.ShouldBe("protocol-error");
        SigilGatewayErrors.Cancelled.ShouldBe("cancelled");
    }
}
