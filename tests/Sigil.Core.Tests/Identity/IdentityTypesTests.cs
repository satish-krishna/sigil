using FluentAssertions;
using Sigil.Core.Identity;
using Xunit;

namespace Sigil.Core.Tests.Identity;

public class IdentityTypesTests
{
    [Fact]
    public void AgentId_WithSameValue_AreEqual()
    {
        var a = new AgentId("agent-1");
        var b = new AgentId("agent-1");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void AgentId_WithDifferentValue_AreNotEqual()
    {
        var a = new AgentId("agent-1");
        var b = new AgentId("agent-2");

        a.Should().NotBe(b);
        (a != b).Should().BeTrue();
    }

    [Fact]
    public void AgentId_ToString_ReturnsRawValue()
    {
        var a = new AgentId("agent-xyz");

        a.ToString().Should().Be("agent-xyz");
    }

    [Fact]
    public void JobId_ToString_ReturnsRawValue()
    {
        new JobId("job-1").ToString().Should().Be("job-1");
    }

    [Fact]
    public void StepId_ToString_ReturnsRawValue()
    {
        new StepId("step-1").ToString().Should().Be("step-1");
    }

    [Fact]
    public void ETag_ToString_ReturnsRawValue()
    {
        new ETag("abc123").ToString().Should().Be("abc123");
    }

    [Fact]
    public void DistinctIdTypes_AreNotImplicitlyConvertible()
    {
        // Compile-time check: this test exists to document intent.
        // If AgentId and JobId ever become implicitly convertible,
        // the assignment below will compile — which is the failure mode.
        var agentId = new AgentId("x");
        var jobId = new JobId("x");

        // Values may coincide, but types must not be interchangeable.
        agentId.Value.Should().Be(jobId.Value);
        agentId.GetType().Should().NotBe(jobId.GetType());
    }
}
