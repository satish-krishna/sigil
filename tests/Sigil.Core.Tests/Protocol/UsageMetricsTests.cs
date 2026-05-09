using Shouldly;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class UsageMetricsTests
{
    [Fact]
    public void Default_HasZeroTokensAndEmptyCustom()
    {
        var m = new UsageMetrics();

        m.PromptTokens.ShouldBe(0);
        m.CompletionTokens.ShouldBe(0);
        m.Duration.ShouldBe(TimeSpan.Zero);
        m.Custom.ShouldBeEmpty();
    }

    [Fact]
    public void InitProperties_Roundtrip()
    {
        var m = new UsageMetrics
        {
            PromptTokens = 10,
            CompletionTokens = 20,
            Duration = TimeSpan.FromSeconds(1.5),
            Custom = new Dictionary<string, object> { ["model"] = "claude-sonnet-4-6" }
        };

        m.PromptTokens.ShouldBe(10);
        m.CompletionTokens.ShouldBe(20);
        m.Duration.ShouldBe(TimeSpan.FromSeconds(1.5));
        m.Custom.Keys.ShouldContain("model");
    }

    [Fact]
    public void TwoMetrics_FromIndependentConstruction_AreEqual()
    {
        var a = new UsageMetrics
        {
            PromptTokens = 10,
            CompletionTokens = 20,
            Duration = TimeSpan.FromSeconds(1.5),
            Custom = new Dictionary<string, object> { ["model"] = "claude-sonnet-4-6" }
        };
        var b = new UsageMetrics
        {
            PromptTokens = 10,
            CompletionTokens = 20,
            Duration = TimeSpan.FromSeconds(1.5),
            Custom = new Dictionary<string, object> { ["model"] = "claude-sonnet-4-6" }
        };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void MetricsDifferingInCustomValue_AreNotEqual()
    {
        var a = new UsageMetrics { Custom = new Dictionary<string, object> { ["model"] = "claude-opus-4-7" } };
        var b = new UsageMetrics { Custom = new Dictionary<string, object> { ["model"] = "claude-sonnet-4-6" } };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void MetricsDifferingInScalarFields_AreNotEqual()
    {
        var a = new UsageMetrics { PromptTokens = 10 };
        var b = new UsageMetrics { PromptTokens = 11 };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void HashCode_IsOrderIndependent_ForCustom()
    {
        var a = new UsageMetrics
        {
            Custom = new Dictionary<string, object> { ["a"] = 1, ["b"] = 2 }
        };
        var b = new UsageMetrics
        {
            Custom = new Dictionary<string, object> { ["b"] = 2, ["a"] = 1 }
        };

        a.GetHashCode().ShouldBe(b.GetHashCode());
        a.ShouldBe(b);
    }
}
