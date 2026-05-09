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

    public bool Equals(AgentRegistration? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return AgentId == other.AgentId
            && Name == other.Name
            && Domain == other.Domain
            && EndpointUrl == other.EndpointUrl
            && SemanticVersion == other.SemanticVersion
            && RoutingWeight == other.RoutingWeight
            && Status == other.Status
            && Model == other.Model
            && Skills.SequenceEqual(other.Skills)
            && Tools.SequenceEqual(other.Tools)
            && MaxTokenBudget == other.MaxTokenBudget
            && Security == other.Security
            && Metadata == other.Metadata
            && RegisteredAt == other.RegisteredAt
            && LastHeartbeat == other.LastHeartbeat;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(AgentId);
        hash.Add(Name);
        hash.Add(Domain);
        hash.Add(EndpointUrl);
        hash.Add(SemanticVersion);
        hash.Add(RoutingWeight);
        hash.Add(Status);
        hash.Add(Model);
        foreach (var skill in Skills) hash.Add(skill);
        foreach (var tool in Tools) hash.Add(tool);
        hash.Add(MaxTokenBudget);
        hash.Add(Security);
        hash.Add(Metadata);
        hash.Add(RegisteredAt);
        hash.Add(LastHeartbeat);
        return hash.ToHashCode();
    }
}
