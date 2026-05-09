using Sigil.Core.Identity;

namespace Sigil.Core.Registry;

public sealed record AgentRegistration
{
    public AgentId AgentId { get; init; }
    public required string Name { get; init; }
    public required string Domain { get; init; }
    public required string EndpointUrl { get; init; }
    public string SemanticVersion { get; init; } = "1.0.0";
    public int RoutingWeight { get; init; } = 100;
    public AgentStatus Status { get; init; } = AgentStatus.Starting;

    public required ModelSpec Model { get; init; }
    public IReadOnlyList<Skill> Skills { get; init; } = [];
    public IReadOnlyList<ToolBinding> Tools { get; init; } = [];
    public int? MaxTokenBudget { get; init; }

    public SecurityProfile Security { get; init; } = new();
    public AgentMetadata Metadata { get; init; } = new();
    public DateTime RegisteredAt { get; init; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; init; } = DateTime.UtcNow;
}
