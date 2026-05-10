using Shouldly;
using Sigil.Storage.EfCore.Internal;
using Xunit;

namespace Sigil.Storage.EfCore.Tests;

public class JsonValueConvertersTests
{
    [Fact]
    public void ReadOnlyList_RoundTrips_Preserving_Order()
    {
        var conv = JsonValueConverters.ReadOnlyListConverter<string>();
        IReadOnlyList<string> input = new[] { "a", "b", "c" };
        var serialized = conv.ConvertToProvider(input)!.ToString();
        var roundTripped = (IReadOnlyList<string>)conv.ConvertFromProvider(serialized)!;
        roundTripped.ShouldBe(input);
    }

    [Fact]
    public void StringArray_RoundTrips()
    {
        var conv = JsonValueConverters.StringArrayConverter();
        var input = new[] { "x", "y" };
        var serialized = conv.ConvertToProvider(input)!.ToString();
        var roundTripped = (string[])conv.ConvertFromProvider(serialized)!;
        roundTripped.ShouldBe(input);
    }

    [Fact]
    public void StringMap_RoundTrips()
    {
        var conv = JsonValueConverters.StringMapConverter();
        IReadOnlyDictionary<string, string> input = new Dictionary<string, string>
        {
            ["team"] = "platform",
            ["tier"] = "open"
        };
        var serialized = conv.ConvertToProvider(input)!.ToString();
        var roundTripped = (IReadOnlyDictionary<string, string>)conv.ConvertFromProvider(serialized)!;
        roundTripped["team"].ShouldBe("platform");
        roundTripped["tier"].ShouldBe("open");
        roundTripped.Count.ShouldBe(2);
    }

    [Fact]
    public void ReadOnlyListComparer_DetectsContentDifference()
    {
        var cmp = JsonValueConverters.ReadOnlyListComparer<string>();
        IReadOnlyList<string> a = new[] { "a", "b" };
        IReadOnlyList<string> b = new[] { "a", "c" };
        cmp.Equals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void StringMapComparer_DetectsKeyAdded()
    {
        var cmp = JsonValueConverters.StringMapComparer();
        IReadOnlyDictionary<string, string> a = new Dictionary<string, string> { ["k"] = "v" };
        IReadOnlyDictionary<string, string> b = new Dictionary<string, string>
        {
            ["k"] = "v",
            ["k2"] = "v2"
        };
        cmp.Equals(a, b).ShouldBeFalse();
    }

    [Fact]
    public void StringMapComparer_OrderInsensitiveEquality()
    {
        var cmp = JsonValueConverters.StringMapComparer();
        IReadOnlyDictionary<string, string> a = new Dictionary<string, string>
        {
            ["a"] = "1",
            ["b"] = "2"
        };
        IReadOnlyDictionary<string, string> b = new Dictionary<string, string>
        {
            ["b"] = "2",
            ["a"] = "1"
        };
        cmp.Equals(a, b).ShouldBeTrue();
    }
}
