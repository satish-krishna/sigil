using Shouldly;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class ContextDeltaTests
{
    [Fact]
    public void Default_HasEmptyUpdatesAndRemovals()
    {
        var delta = new ContextDelta();

        delta.Updates.ShouldBeEmpty();
        delta.Removals.ShouldBeEmpty();
    }

    [Fact]
    public void CanPopulateUpdatesAfterConstruction()
    {
        var delta = new ContextDelta();
        delta.Updates["key"] = "value";

        delta.Updates.ShouldContainKey("key");
    }

    [Fact]
    public void TwoDeltas_FromIndependentConstruction_AreEqual()
    {
        var c = new ContextDelta
        {
            Updates = new Dictionary<string, object> { ["k"] = "v" },
            Removals = ["r"]
        };
        var d = new ContextDelta
        {
            Updates = new Dictionary<string, object> { ["k"] = "v" },
            Removals = ["r"]
        };

        c.ShouldBe(d);
        c.GetHashCode().ShouldBe(d.GetHashCode());
    }

    [Fact]
    public void DeltasDifferingInUpdates_AreNotEqual()
    {
        var c = new ContextDelta { Updates = new Dictionary<string, object> { ["k"] = "v1" } };
        var d = new ContextDelta { Updates = new Dictionary<string, object> { ["k"] = "v2" } };

        c.ShouldNotBe(d);
    }

    [Fact]
    public void DeltasDifferingInRemovals_AreNotEqual()
    {
        var c = new ContextDelta { Removals = ["a"] };
        var d = new ContextDelta { Removals = ["b"] };

        c.ShouldNotBe(d);
    }

    [Fact]
    public void DeltasDifferingInRemovalOrder_AreNotEqual()
    {
        // Removals are an ordered sequence — JSON round-trip preserves order.
        var c = new ContextDelta { Removals = ["a", "b"] };
        var d = new ContextDelta { Removals = ["b", "a"] };

        c.ShouldNotBe(d);
    }
}
