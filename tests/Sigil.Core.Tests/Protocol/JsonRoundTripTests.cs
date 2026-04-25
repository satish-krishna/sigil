using System.Text.Json;
using FluentAssertions;
using Sigil.Core.Audit;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Xunit;

namespace Sigil.Core.Tests.Protocol;

public class JsonRoundTripTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        IncludeFields = false
    };

    [Fact]
    public void AgentId_RoundTrips()
    {
        var original = new AgentId("agent-1");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<AgentId>(json, Options);

        back.Should().Be(original);
    }

    [Fact]
    public void ContextDelta_RoundTrips()
    {
        var original = new ContextDelta
        {
            Updates = new Dictionary<string, object> { ["k"] = "v" },
            Removals = ["r1", "r2"]
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<ContextDelta>(json, Options)!;

        back.Updates.Should().ContainKey("k");
        back.Removals.Should().Equal("r1", "r2");
    }

    [Fact]
    public void UsageMetrics_RoundTrips()
    {
        var original = new UsageMetrics
        {
            PromptTokens = 100,
            CompletionTokens = 200,
            Duration = TimeSpan.FromSeconds(2.5)
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<UsageMetrics>(json, Options)!;

        back.PromptTokens.Should().Be(100);
        back.CompletionTokens.Should().Be(200);
        back.Duration.Should().Be(TimeSpan.FromSeconds(2.5));
    }

    [Fact]
    public void AgentLogEntry_RoundTrips()
    {
        var original = new AgentLogEntry
        {
            Timestamp = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc),
            AgentId = new AgentId("agent-1"),
            Level = "Info",
            Message = "hello"
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<AgentLogEntry>(json, Options)!;

        back.Timestamp.Should().Be(original.Timestamp);
        back.AgentId.Should().Be(original.AgentId);
        back.Level.Should().Be("Info");
        back.Message.Should().Be("hello");
    }

    [Fact]
    public void AuditEntry_RoundTrips()
    {
        var original = new AuditEntry
        {
            AuditId = "fixed-audit-id",
            JobId = new JobId("j-1"),
            AgentId = new AgentId("a-1"),
            StepId = new StepId("s-1"),
            Delta = new ContextDelta { Removals = ["k"] },
            Metrics = new UsageMetrics { PromptTokens = 5 },
            Timestamp = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<AuditEntry>(json, Options)!;

        back.AuditId.Should().Be("fixed-audit-id");
        back.JobId.Should().Be(original.JobId);
        back.AgentId.Should().Be(original.AgentId);
        back.StepId.Should().Be(original.StepId);
        back.Delta.Removals.Should().Equal("k");
        back.Metrics.PromptTokens.Should().Be(5);
        back.Timestamp.Should().Be(original.Timestamp);
    }

    [Fact]
    public void JobId_RoundTrips()
    {
        var original = new JobId("job-1");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<JobId>(json, Options);

        back.Should().Be(original);
    }

    [Fact]
    public void StepId_RoundTrips()
    {
        var original = new StepId("step-1");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<StepId>(json, Options);

        back.Should().Be(original);
    }

    [Fact]
    public void ETag_RoundTrips()
    {
        var original = new ETag("etag-abc");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<ETag>(json, Options);

        back.Should().Be(original);
    }

    [Fact]
    public void IdentityTypes_RejectJsonNull()
    {
        Action agentId = () => JsonSerializer.Deserialize<AgentId>("null", Options);
        Action jobId = () => JsonSerializer.Deserialize<JobId>("null", Options);
        Action stepId = () => JsonSerializer.Deserialize<StepId>("null", Options);
        Action eTag = () => JsonSerializer.Deserialize<ETag>("null", Options);

        agentId.Should().Throw<JsonException>().WithMessage("*null*AgentId*");
        jobId.Should().Throw<JsonException>().WithMessage("*null*JobId*");
        stepId.Should().Throw<JsonException>().WithMessage("*null*StepId*");
        eTag.Should().Throw<JsonException>().WithMessage("*null*ETag*");
    }
}
