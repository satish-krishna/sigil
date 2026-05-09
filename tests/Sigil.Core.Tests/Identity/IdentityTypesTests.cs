using Shouldly;
using Sigil.Core.Identity;
using Xunit;

namespace Sigil.Core.Tests.Identity;

public class IdentityTypesTests
{
    public static IEnumerable<object[]> IdentityFactories() =>
        new List<object[]>
        {
            new object[] { (Func<string, object>)(v => new AgentId(v)) },
            new object[] { (Func<string, object>)(v => new JobId(v)) },
            new object[] { (Func<string, object>)(v => new StepId(v)) },
            new object[] { (Func<string, object>)(v => new ETag(v)) }
        };

    [Theory]
    [MemberData(nameof(IdentityFactories))]
    public void Identity_WithSameValue_AreEqual(Func<string, object> make)
    {
        var a = make("x");
        var b = make("x");

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Theory]
    [MemberData(nameof(IdentityFactories))]
    public void Identity_WithDifferentValue_AreNotEqual(Func<string, object> make)
    {
        var a = make("x");
        var b = make("y");

        a.ShouldNotBe(b);
    }

    [Fact]
    public void AgentId_ToString_ReturnsRawValue()
    {
        var a = new AgentId("agent-xyz");

        a.ToString().ShouldBe("agent-xyz");
    }

    [Fact]
    public void JobId_ToString_ReturnsRawValue()
    {
        new JobId("job-1").ToString().ShouldBe("job-1");
    }

    [Fact]
    public void StepId_ToString_ReturnsRawValue()
    {
        new StepId("step-1").ToString().ShouldBe("step-1");
    }

    [Fact]
    public void ETag_ToString_ReturnsRawValue()
    {
        new ETag("abc123").ToString().ShouldBe("abc123");
    }
}
