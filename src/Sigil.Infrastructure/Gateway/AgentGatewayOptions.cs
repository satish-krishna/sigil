namespace Sigil.Infrastructure.Gateway;

public sealed class AgentGatewayOptions
{
    public const string SectionName = "Gateway";

    public TimeSpan ValidateTimeout { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ExecuteTimeout { get; set; } = TimeSpan.FromSeconds(120);

    public int MaxRetryAttempts { get; set; } = 2;
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    public int CircuitBreakerFailureRatio { get; set; } = 50;     // percent
    public int CircuitBreakerMinimumThroughput { get; set; } = 10; // calls in window
    public TimeSpan CircuitBreakerSamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CircuitBreakerBreakDuration { get; set; } = TimeSpan.FromSeconds(15);
}
