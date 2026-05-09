namespace Sigil.Core.Protocol;

public sealed record ValidationResult
{
    public bool CanHandle { get; init; }
    public int? EstimatedTokens { get; init; }
    public IReadOnlyList<string> MissingTools { get; init; } = [];
    public string? Reason { get; init; }

    public bool Equals(ValidationResult? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return CanHandle == other.CanHandle
            && EstimatedTokens == other.EstimatedTokens
            && Reason == other.Reason
            && MissingTools.SequenceEqual(other.MissingTools);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CanHandle);
        hash.Add(EstimatedTokens);
        hash.Add(Reason);
        foreach (var tool in MissingTools) hash.Add(tool);
        return hash.ToHashCode();
    }
}
