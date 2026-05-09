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
    public void DeltasWithSameReferenceInstances_AreEqualByRecordSemantics()
    {
        // Record equality compares reference-type properties by reference, not by content.
        // Two records are equal only when they hold the *same* dictionary/array instances.
        var shared = new Dictionary<string, object> { ["k"] = "v" };
        var sharedR = new[] { "r" };
        var c = new ContextDelta { Updates = shared, Removals = sharedR };
        var d = new ContextDelta { Updates = shared, Removals = sharedR };

        c.ShouldBe(d);
    }
}
