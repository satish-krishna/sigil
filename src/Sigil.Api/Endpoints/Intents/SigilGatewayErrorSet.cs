using Sigil.Core.Gateway;

namespace Sigil.Api.Endpoints.Intents;

internal static class SigilGatewayErrorSet
{
    public static readonly HashSet<string> All = new(StringComparer.Ordinal)
    {
        SigilGatewayErrors.TierNotSupported,
        SigilGatewayErrors.OutboundKeyMissing,
        SigilGatewayErrors.EndpointInvalid,
        SigilGatewayErrors.AgentRejectedCredentials,
        SigilGatewayErrors.AgentNotFound,
        SigilGatewayErrors.AgentRejected,
        SigilGatewayErrors.AgentError,
        SigilGatewayErrors.Timeout,
        SigilGatewayErrors.CircuitOpen,
        SigilGatewayErrors.TransportError,
        SigilGatewayErrors.ProtocolError,
        SigilGatewayErrors.Cancelled,
    };
}
