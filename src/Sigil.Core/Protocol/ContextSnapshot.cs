using CSharpFunctionalExtensions;
using Sigil.Core.Identity;

namespace Sigil.Core.Protocol;

public sealed record ContextSnapshot
{
    public JobId JobId { get; init; }

    public IReadOnlyDictionary<string, object> State { get; init; }
        = new Dictionary<string, object>();

    public Maybe<T> Get<T>(string key) =>
        State.TryGetValue(key, out var val) && val is T typed
            ? Maybe.From(typed)
            : Maybe<T>.None;
}
