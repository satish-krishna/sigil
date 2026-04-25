using Sigil.Core.Identity;

namespace Sigil.Core.Registry;

public sealed record AgentRegistration
{
    public AgentId AgentId { get; init; }
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public IReadOnlyList<Capability> Capabilities { get; init; } = [];
    public string SemanticVersion { get; init; } = "1.0.0";
    public required string EndpointUrl { get; init; }
    public int RoutingWeight { get; init; } = 100;
    public AgentStatus Status { get; init; } = AgentStatus.Starting;
    public SecurityProfile Security { get; init; } = new();
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; init; } = DateTime.UtcNow;
    public AgentMetadata Metadata { get; init; } = new();
}
