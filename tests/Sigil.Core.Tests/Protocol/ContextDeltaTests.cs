using FluentAssertions;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class ContextDeltaTests
{
    [Fact]
    public void Default_HasEmptyUpdatesAndRemovals()
    {
        var delta = new ContextDelta();

        delta.Updates.Should().BeEmpty();
        delta.Removals.Should().BeEmpty();
    }

    [Fact]
    public void CanPopulateUpdatesAfterConstruction()
    {
        var delta = new ContextDelta();
        delta.Updates["key"] = "value";

        delta.Updates.Should().ContainKey("key");
    }

    [Fact]
    public void DeltasWithSameContent_AreEqualByRecordSemantics()
    {
        var shared = new Dictionary<string, object> { ["k"] = "v" };
        var sharedR = new[] { "r" };
        var c = new ContextDelta { Updates = shared, Removals = sharedR };
        var d = new ContextDelta { Updates = shared, Removals = sharedR };

        c.Should().Be(d);
    }
}
