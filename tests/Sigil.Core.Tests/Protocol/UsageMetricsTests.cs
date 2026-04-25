using FluentAssertions;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class UsageMetricsTests
{
    [Fact]
    public void Default_HasZeroTokensAndEmptyCustom()
    {
        var m = new UsageMetrics();

        m.PromptTokens.Should().Be(0);
        m.CompletionTokens.Should().Be(0);
        m.Duration.Should().Be(TimeSpan.Zero);
        m.Custom.Should().BeEmpty();
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

        m.PromptTokens.Should().Be(10);
        m.CompletionTokens.Should().Be(20);
        m.Duration.Should().Be(TimeSpan.FromSeconds(1.5));
        m.Custom.Should().ContainKey("model");
    }
}
