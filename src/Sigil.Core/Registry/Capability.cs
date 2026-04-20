namespace Sigil.Core.Registry;

public sealed record Capability
{
    public string Name { get; init; } = default!;
    public string? Description { get; init; }
    public string[] RequiredTools { get; init; } = [];
    public int? EstimatedMaxTokens { get; init; }
}
