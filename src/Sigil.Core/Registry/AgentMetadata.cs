namespace Sigil.Core.Registry;

public sealed record AgentMetadata
{
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();

    public bool Equals(AgentMetadata? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (Tags.Count != other.Tags.Count) return false;
        foreach (var kvp in Tags)
        {
            if (!other.Tags.TryGetValue(kvp.Key, out var v) || v != kvp.Value)
                return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        // Sort keys for order-independent hash code.
        foreach (var kvp in Tags.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }
}
