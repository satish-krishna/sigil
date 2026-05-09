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
}
