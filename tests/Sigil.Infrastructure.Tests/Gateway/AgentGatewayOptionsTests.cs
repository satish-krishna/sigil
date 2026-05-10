using Shouldly;
using Sigil.Infrastructure.Gateway;
using Xunit;

namespace Sigil.Infrastructure.Tests.Gateway;

public class AgentGatewayOptionsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var opts = new AgentGatewayOptions();

        opts.ValidateTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        opts.ExecuteTimeout.ShouldBe(TimeSpan.FromSeconds(120));
        opts.MaxRetryAttempts.ShouldBe(2);
        opts.BaseRetryDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
        opts.CircuitBreakerFailureRatio.ShouldBe(50);
        opts.CircuitBreakerMinimumThroughput.ShouldBe(10);
        opts.CircuitBreakerSamplingDuration.ShouldBe(TimeSpan.FromSeconds(30));
        opts.CircuitBreakerBreakDuration.ShouldBe(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public void SectionName_Is_Gateway()
    {
        AgentGatewayOptions.SectionName.ShouldBe("Gateway");
    }
}
