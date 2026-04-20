namespace Sigil.Core.Registry;

public sealed record AgentMetadata
{
    public int? MaxTokenBudget { get; init; }
    public string? Model { get; init; }
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();
}
