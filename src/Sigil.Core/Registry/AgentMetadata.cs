namespace Sigil.Core.Registry;

public sealed record AgentMetadata
{
    public IReadOnlyDictionary<string, string> Tags { get; init; }
        = new Dictionary<string, string>();
}
