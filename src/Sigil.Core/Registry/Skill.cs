namespace Sigil.Core.Registry;

public sealed record Skill
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> RequiredTools { get; init; } = [];
    public int? EstimatedMaxTokens { get; init; }
    public string Version { get; init; } = "1.0.0";

    public bool Equals(Skill? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Name == other.Name
            && Description == other.Description
            && RequiredTools.SequenceEqual(other.RequiredTools)
            && EstimatedMaxTokens == other.EstimatedMaxTokens
            && Version == other.Version;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(Description);
        foreach (var tool in RequiredTools) hash.Add(tool);
        hash.Add(EstimatedMaxTokens);
        hash.Add(Version);
        return hash.ToHashCode();
    }
}
