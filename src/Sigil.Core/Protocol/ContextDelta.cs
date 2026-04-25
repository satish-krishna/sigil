namespace Sigil.Core.Protocol;

public sealed record ContextDelta
{
    public Dictionary<string, object> Updates { get; init; } = new();
    public string[] Removals { get; init; } = [];
}
