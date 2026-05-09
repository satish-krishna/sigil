namespace Sigil.Core.Protocol;

public sealed record UsageMetrics
{
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyDictionary<string, object> Custom { get; init; }
        = new Dictionary<string, object>();

    public bool Equals(UsageMetrics? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (PromptTokens != other.PromptTokens) return false;
        if (CompletionTokens != other.CompletionTokens) return false;
        if (Duration != other.Duration) return false;
        if (Custom.Count != other.Custom.Count) return false;
        foreach (var kvp in Custom)
        {
            if (!other.Custom.TryGetValue(kvp.Key, out var v) || !Equals(kvp.Value, v))
                return false;
        }
        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(PromptTokens);
        hash.Add(CompletionTokens);
        hash.Add(Duration);
        // Sort keys for order-independent hash code.
        foreach (var kvp in Custom.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }
}
