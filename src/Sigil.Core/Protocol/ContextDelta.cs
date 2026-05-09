namespace Sigil.Core.Protocol;

public sealed record ContextDelta
{
    public Dictionary<string, object> Updates { get; init; } = new();
    public string[] Removals { get; init; } = [];

    public bool Equals(ContextDelta? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (!Removals.SequenceEqual(other.Removals)) return false;
        if (Updates.Count != other.Updates.Count) return false;
        foreach (var kvp in Updates)
        {
            if (!other.Updates.TryGetValue(kvp.Key, out var v) || !Equals(kvp.Value, v))
                return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var r in Removals) hash.Add(r);
        // Sort keys for order-independent hash code.
        foreach (var kvp in Updates.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }
}
