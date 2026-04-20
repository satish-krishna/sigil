namespace Sigil.Core.Registry;

public sealed record Capability
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string[] RequiredTools { get; init; } = [];
    public int? EstimatedMaxTokens { get; init; }
}
