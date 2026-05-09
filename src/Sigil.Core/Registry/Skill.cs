namespace Sigil.Core.Registry;

public sealed record Skill
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<string> RequiredTools { get; init; } = [];
    public int? EstimatedMaxTokens { get; init; }
    public string Version { get; init; } = "1.0.0";
}
