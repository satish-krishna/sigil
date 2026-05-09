using Sigil.Core.Identity;

namespace Sigil.Core.Security;

public sealed record AuthenticationResult
{
    public required AgentId AgentId { get; init; }
    public required SecurityTier Tier { get; init; }

    public bool Equals(AuthenticationResult? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return AgentId == other.AgentId && Tier == other.Tier;
    }

    public override int GetHashCode() => HashCode.Combine(AgentId, Tier);
}
