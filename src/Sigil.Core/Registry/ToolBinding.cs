namespace Sigil.Core.Registry;

public sealed record ToolBinding
{
    public required string Name { get; init; }
    public required ToolKind Kind { get; init; }
    public required string Description { get; init; }
    public required string ParameterSchema { get; init; }
}

public enum ToolKind
{
    Mcp,
    Http,
    InProcess
}
