using System.Text.Json;
using Shouldly;
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

        back.ShouldBe(original);
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

        back.Updates.ShouldContainKey("k");
        back.Removals.ShouldBe(new[] { "r1", "r2" });
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

        back.PromptTokens.ShouldBe(100);
        back.CompletionTokens.ShouldBe(200);
        back.Duration.ShouldBe(TimeSpan.FromSeconds(2.5));
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

        back.Timestamp.ShouldBe(original.Timestamp);
        back.AgentId.ShouldBe(original.AgentId);
        back.Level.ShouldBe("Info");
        back.Message.ShouldBe("hello");
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

        back.AuditId.ShouldBe("fixed-audit-id");
        back.JobId.ShouldBe(original.JobId);
        back.AgentId.ShouldBe(original.AgentId);
        back.StepId.ShouldBe(original.StepId);
        back.Delta.Removals.ShouldBe(new[] { "k" });
        back.Metrics.PromptTokens.ShouldBe(5);
        back.Timestamp.ShouldBe(original.Timestamp);
    }

    [Fact]
    public void JobId_RoundTrips()
    {
        var original = new JobId("job-1");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<JobId>(json, Options);

        back.ShouldBe(original);
    }

    [Fact]
    public void StepId_RoundTrips()
    {
        var original = new StepId("step-1");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<StepId>(json, Options);

        back.ShouldBe(original);
    }

    [Fact]
    public void ETag_RoundTrips()
    {
        var original = new ETag("etag-abc");

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<ETag>(json, Options);

        back.ShouldBe(original);
    }

    [Fact]
    public void IdentityTypes_RejectJsonNull()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<AgentId>("null", Options))
            .Message.ShouldContain("AgentId");
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<JobId>("null", Options))
            .Message.ShouldContain("JobId");
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<StepId>("null", Options))
            .Message.ShouldContain("StepId");
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ETag>("null", Options))
            .Message.ShouldContain("ETag");
    }
}
