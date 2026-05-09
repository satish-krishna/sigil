using CSharpFunctionalExtensions;
using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class ContextSnapshotTests
{
    [Fact]
    public void Default_StateIsEmpty()
    {
        var snap = new ContextSnapshot();

        snap.State.ShouldBeEmpty();
    }

    [Fact]
    public void JobId_RoundTrips()
    {
        var snap = new ContextSnapshot { JobId = new JobId("job-1") };

        snap.JobId.ShouldBe(new JobId("job-1"));
    }

    [Fact]
    public void Get_WhenKeyPresentAndTypeMatches_ReturnsMaybeFromValue()
    {
        var snap = new ContextSnapshot
        {
            State = new Dictionary<string, object> { ["count"] = 42 }
        };

        var result = snap.Get<int>("count");

        result.HasValue.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void Get_WhenKeyAbsent_ReturnsMaybeNone()
    {
        var snap = new ContextSnapshot();

        var result = snap.Get<int>("missing");

        result.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public void Get_WhenKeyPresentButWrongType_ReturnsMaybeNone()
    {
        var snap = new ContextSnapshot
        {
            State = new Dictionary<string, object> { ["count"] = "not-an-int" }
        };

        var result = snap.Get<int>("count");

        result.HasNoValue.ShouldBeTrue();
    }

    [Fact]
    public void State_IsReadOnlyDictionary()
    {
        typeof(ContextSnapshot).GetProperty("State")!.PropertyType
            .ShouldBe(typeof(IReadOnlyDictionary<string, object>));
    }
}
