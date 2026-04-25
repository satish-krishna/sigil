using System.Collections.ObjectModel;
using System.Text.Json;
using CSharpFunctionalExtensions;
using Sigil.Core.Identity;

namespace Sigil.Core.Protocol;

public sealed record ContextSnapshot
{
    private readonly IReadOnlyDictionary<string, object> _state =
        new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

    public JobId JobId { get; init; }

    // Defensive copy on init so callers cannot mutate State by retaining the source dictionary.
    public IReadOnlyDictionary<string, object> State
    {
        get => _state;
        init => _state = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(value));
    }

    public Maybe<T> Get<T>(string key)
    {
        if (!State.TryGetValue(key, out var val))
            return Maybe<T>.None;

        if (val is T typed)
            return Maybe.From(typed);

        // After JSON round-trip, values are JsonElement; deserialize to the requested type.
        if (val is JsonElement element)
        {
            var deserialized = element.Deserialize<T>();
            return deserialized is not null ? Maybe.From(deserialized) : Maybe<T>.None;
        }

        return Maybe<T>.None;
    }
}
