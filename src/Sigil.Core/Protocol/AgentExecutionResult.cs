namespace Sigil.Core.Protocol;

public sealed record AgentExecutionResult
{
    public required ContextDelta Delta { get; init; }
    public IReadOnlyList<AgentLogEntry> Logs { get; init; } = [];
    public UsageMetrics Metrics { get; init; } = new();

    public bool Equals(AgentExecutionResult? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Delta == other.Delta
            && Logs.SequenceEqual(other.Logs)
            && Metrics == other.Metrics;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Delta);
        foreach (var entry in Logs) hash.Add(entry);
        hash.Add(Metrics);
        return hash.ToHashCode();
    }
}
