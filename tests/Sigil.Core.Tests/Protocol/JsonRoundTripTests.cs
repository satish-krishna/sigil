using System.Text.Json;
using Shouldly;
using Sigil.Core.Audit;
using Sigil.Core.Identity;
using Sigil.Core.Protocol;
using Sigil.Core.Registry;
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
        var agentEx = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<AgentId>("null", Options));
        agentEx.Message.ShouldContain("null");
        agentEx.Message.ShouldContain("AgentId");

        var jobEx = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<JobId>("null", Options));
        jobEx.Message.ShouldContain("null");
        jobEx.Message.ShouldContain("JobId");

        var stepEx = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<StepId>("null", Options));
        stepEx.Message.ShouldContain("null");
        stepEx.Message.ShouldContain("StepId");

        var etagEx = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<ETag>("null", Options));
        etagEx.Message.ShouldContain("null");
        etagEx.Message.ShouldContain("ETag");
    }

    [Fact]
    public void Skill_RoundTrips()
    {
        var original = new Skill
        {
            Name = "summarize-pdf",
            Description = "Summarize a PDF.",
            RequiredTools = new[] { "fetch_pdf", "extract_text" },
            EstimatedMaxTokens = 800,
            Version = "1.2.0"
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<Skill>(json, Options)!;

        back.Name.ShouldBe(original.Name);
        back.Description.ShouldBe(original.Description);
        back.RequiredTools.ShouldBe(original.RequiredTools);
        back.EstimatedMaxTokens.ShouldBe(original.EstimatedMaxTokens);
        back.Version.ShouldBe(original.Version);
    }

    [Fact]
    public void ModelSpec_RoundTrips()
    {
        var original = new ModelSpec
        {
            Provider = "openai",
            Model = "gpt-4o-mini",
            Sampling = new Sampling
            {
                Temperature = 0.2,
                TopP = 0.9,
                MaxOutputTokens = 800
            }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<ModelSpec>(json, Options)!;

        back.ShouldBe(original);
    }

    [Fact]
    public void ToolBinding_RoundTrips()
    {
        var original = new ToolBinding
        {
            Name = "get_forecast",
            Kind = ToolKind.Http,
            Description = "Fetch a 7-day forecast.",
            ParameterSchema = "{\"type\":\"object\"}"
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<ToolBinding>(json, Options)!;

        back.ShouldBe(original);
    }

    [Fact]
    public void AgentMetadata_RoundTrips()
    {
        var original = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform", ["tier"] = "standard" }
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<AgentMetadata>(json, Options)!;

        back.Tags.ContainsKey("team").ShouldBeTrue();
        back.Tags["team"].ShouldBe("platform");
        back.Tags.ContainsKey("tier").ShouldBeTrue();
        back.Tags["tier"].ShouldBe("standard");
    }

    [Fact]
    public void AgentRegistration_RoundTrips()
    {
        var original = new AgentRegistration
        {
            AgentId = new AgentId("weather-bot"),
            Name = "Weather Bot",
            Domain = "weather",
            EndpointUrl = "https://weather-bot.internal:8443",
            SemanticVersion = "1.0.0",
            RoutingWeight = 100,
            Status = AgentStatus.Healthy,
            Model = new ModelSpec
            {
                Provider = "openai",
                Model = "gpt-4o-mini",
                Sampling = new Sampling { Temperature = 0.2, MaxOutputTokens = 800 }
            },
            Skills =
            [
                new Skill
                {
                    Name = "forecast-summary",
                    Description = "Summarize a forecast.",
                    RequiredTools = new[] { "get_forecast" },
                    EstimatedMaxTokens = 400
                }
            ],
            Tools =
            [
                new ToolBinding
                {
                    Name = "get_forecast",
                    Kind = ToolKind.Http,
                    Description = "Fetch a 7-day forecast.",
                    ParameterSchema = "{\"type\":\"object\"}"
                }
            ],
            MaxTokenBudget = 4000,
            Security = new SecurityProfile { AllowedTools = new[] { "get_forecast" } },
            Metadata = new AgentMetadata
            {
                Tags = new Dictionary<string, string> { ["team"] = "platform" }
            },
            RegisteredAt = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc),
            LastHeartbeat = new DateTime(2026, 5, 9, 12, 1, 0, DateTimeKind.Utc)
        };

        var json = JsonSerializer.Serialize(original, Options);
        var back = JsonSerializer.Deserialize<AgentRegistration>(json, Options)!;

        back.Name.ShouldBe("Weather Bot");
        back.Skills.Count.ShouldBe(1);
        back.Skills[0].Name.ShouldBe("forecast-summary");
        back.Tools.Count.ShouldBe(1);
        back.Tools[0].Kind.ShouldBe(ToolKind.Http);
        back.Model.Provider.ShouldBe("openai");
        back.MaxTokenBudget.ShouldBe(4000);
        back.Security.AllowedTools.ShouldBe(new[] { "get_forecast" });
        back.Metadata.Tags.ContainsKey("team").ShouldBeTrue();
    }

    [Fact]
    public void ToolKind_SerializesAsString()
    {
        var binding = new ToolBinding
        {
            Name = "x",
            Kind = ToolKind.Http,
            Description = "y",
            ParameterSchema = "{}"
        };

        var json = JsonSerializer.Serialize(binding, Options);

        json.ShouldContain("\"Http\"");
        json.ShouldNotContain("\"Kind\":1");
    }

    [Fact]
    public void AgentStatus_SerializesAsString()
    {
        var registration = new AgentRegistration
        {
            Name = "x",
            Domain = "y",
            EndpointUrl = "https://example",
            Model = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" },
            Status = AgentStatus.Healthy
        };

        var json = JsonSerializer.Serialize(registration, Options);

        json.ShouldContain("\"Healthy\"");
        json.ShouldNotContain("\"Status\":1");
    }
}
