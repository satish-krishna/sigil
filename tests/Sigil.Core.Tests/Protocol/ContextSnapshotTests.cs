using CSharpFunctionalExtensions;
using FluentAssertions;
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

        snap.State.Should().BeEmpty();
    }

    [Fact]
    public void JobId_RoundTrips()
    {
        var snap = new ContextSnapshot { JobId = new JobId("job-1") };

        snap.JobId.Should().Be(new JobId("job-1"));
    }

    [Fact]
    public void Get_WhenKeyPresentAndTypeMatches_ReturnsMaybeFromValue()
    {
        var snap = new ContextSnapshot
        {
            State = new Dictionary<string, object> { ["count"] = 42 }
        };

        var result = snap.Get<int>("count");

        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Get_WhenKeyAbsent_ReturnsMaybeNone()
    {
        var snap = new ContextSnapshot();

        var result = snap.Get<int>("missing");

        result.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void Get_WhenKeyPresentButWrongType_ReturnsMaybeNone()
    {
        var snap = new ContextSnapshot
        {
            State = new Dictionary<string, object> { ["count"] = "not-an-int" }
        };

        var result = snap.Get<int>("count");

        result.HasNoValue.Should().BeTrue();
    }

    [Fact]
    public void State_IsReadOnlyDictionary()
    {
        typeof(ContextSnapshot).GetProperty("State")!.PropertyType
            .Should().Be(typeof(IReadOnlyDictionary<string, object>));
    }
}
