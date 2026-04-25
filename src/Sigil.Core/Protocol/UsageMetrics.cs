namespace Sigil.Core.Protocol;

public sealed record UsageMetrics
{
    public long PromptTokens { get; init; }
    public long CompletionTokens { get; init; }
    public TimeSpan Duration { get; init; }
    public IReadOnlyDictionary<string, object> Custom { get; init; }
        = new Dictionary<string, object>();
}
