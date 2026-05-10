namespace Sigil.Core.Gateway;

public static class SigilGatewayErrors
{
    // Pre-flight (no HTTP attempt)
    public const string TierNotSupported   = "tier-not-supported";
    public const string OutboundKeyMissing = "outbound-key-missing";
    public const string EndpointInvalid    = "endpoint-invalid";

    // HTTP outcomes
    public const string AgentRejectedCredentials = "agent-rejected-credentials"; // 401/403
    public const string AgentNotFound            = "agent-not-found";            // 404
    public const string AgentRejected            = "agent-rejected";             // other 4xx
    public const string AgentError               = "agent-error";                // 5xx after retries

    // Transport / resilience
    public const string Timeout        = "timeout";
    public const string CircuitOpen    = "circuit-open";
    public const string TransportError = "transport-error";

    // Protocol
    public const string ProtocolError = "protocol-error"; // 2xx body fails to deserialize

    // Cancellation
    public const string Cancelled = "cancelled";
}
