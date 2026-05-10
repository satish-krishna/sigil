using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Sigil.Storage.EfCore.Internal;

internal static class JsonValueConverters
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ValueConverter<IReadOnlyList<T>, string> ReadOnlyListConverter<T>() =>
        new(
            v => JsonSerializer.Serialize(v, Json),
            s => JsonSerializer.Deserialize<List<T>>(s, Json) ?? new List<T>());

    public static ValueComparer<IReadOnlyList<T>> ReadOnlyListComparer<T>() =>
        new(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (acc, x) => HashCode.Combine(acc, x)),
            v => v.ToList());

    public static ValueConverter<IReadOnlyDictionary<string, string>, string> StringMapConverter() =>
        new(
            v => JsonSerializer.Serialize(v, Json),
            s => JsonSerializer.Deserialize<Dictionary<string, string>>(s, Json) ?? new());

    public static ValueComparer<IReadOnlyDictionary<string, string>> StringMapComparer() =>
        new(
            (a, b) => DictEquals(a, b, StringComparer.Ordinal),
            v => HashDict(v),
            v => new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(v)));

    public static ValueConverter<IReadOnlyDictionary<string, object>, string> ObjectMapConverter() =>
        new(
            v => JsonSerializer.Serialize(v, Json),
            s => JsonSerializer.Deserialize<Dictionary<string, object>>(s, Json) ?? new());

    public static ValueComparer<IReadOnlyDictionary<string, object>> ObjectMapComparer() =>
        new(
            (a, b) => DictEquals(a, b, EqualityComparer<object>.Default),
            v => HashDict(v),
            v => new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(v)));

    public static ValueConverter<Dictionary<string, object>, string> MutableObjectMapConverter() =>
        new(
            v => JsonSerializer.Serialize(v, Json),
            s => JsonSerializer.Deserialize<Dictionary<string, object>>(s, Json) ?? new());

    public static ValueComparer<Dictionary<string, object>> MutableObjectMapComparer() =>
        new(
            (a, b) => DictEquals(a, b, EqualityComparer<object>.Default),
            v => HashDict(v),
            v => new Dictionary<string, object>(v));

    public static ValueConverter<string[], string> StringArrayConverter() =>
        new(
            v => JsonSerializer.Serialize(v, Json),
            s => JsonSerializer.Deserialize<string[]>(s, Json) ?? Array.Empty<string>());

    public static ValueComparer<string[]> StringArrayComparer() =>
        new(
            (a, b) => (a == null && b == null) || (a != null && b != null && a.SequenceEqual(b)),
            v => v.Aggregate(0, (acc, x) => HashCode.Combine(acc, x)),
            v => v.ToArray());

    private static bool DictEquals<TKey, TVal>(
        IReadOnlyDictionary<TKey, TVal>? a,
        IReadOnlyDictionary<TKey, TVal>? b,
        IEqualityComparer<TVal> valueCmp)
    {
        if (a is null && b is null) return true;
        if (a is null || b is null) return false;
        if (a.Count != b.Count) return false;
        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var bv)) return false;
            if (!valueCmp.Equals(kvp.Value, bv)) return false;
        }
        return true;
    }

    private static int HashDict<TKey, TVal>(IReadOnlyDictionary<TKey, TVal> v)
        where TKey : notnull
    {
        var hash = new HashCode();
        foreach (var kvp in v.OrderBy(k => k.Key.ToString(), StringComparer.Ordinal))
        {
            hash.Add(kvp.Key);
            hash.Add(kvp.Value);
        }
        return hash.ToHashCode();
    }
}
