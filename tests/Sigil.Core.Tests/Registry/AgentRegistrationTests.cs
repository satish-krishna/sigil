using Shouldly;
using Sigil.Core.Identity;
using Sigil.Core.Registry;
using Xunit;

namespace Sigil.Core.Tests.Registry;

public class AgentRegistrationTests
{
    private static AgentRegistration MakeFull() => new()
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
                RequiredTools = ["get_forecast"],
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
        Security = new SecurityProfile { AllowedTools = ["get_forecast"] },
        Metadata = new AgentMetadata
        {
            Tags = new Dictionary<string, string> { ["team"] = "platform" }
        },
        RegisteredAt = new DateTime(2026, 5, 9, 12, 0, 0, DateTimeKind.Utc),
        LastHeartbeat = new DateTime(2026, 5, 9, 12, 1, 0, DateTimeKind.Utc)
    };

    [Fact]
    public void Defaults_HasEmptySkillsAndTools()
    {
        var r = new AgentRegistration
        {
            Name = "x",
            Domain = "y",
            EndpointUrl = "https://example",
            Model = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" }
        };

        r.Skills.ShouldBeEmpty();
        r.Tools.ShouldBeEmpty();
        r.MaxTokenBudget.ShouldBeNull();
        r.RoutingWeight.ShouldBe(100);
        r.Status.ShouldBe(AgentStatus.Starting);
        r.SemanticVersion.ShouldBe("1.0.0");
    }

    [Fact]
    public void TwoRegistrationsWithSameFields_AreEqual()
    {
        var a = MakeFull();
        var b = a with { };

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }

    [Fact]
    public void TwoRegistrationsDifferingInSkills_AreNotEqual()
    {
        var a = MakeFull();
        var b = a with { Skills = [] };

        a.ShouldNotBe(b);
    }

    [Fact]
    public void TwoRegistrationsDifferingInModel_AreNotEqual()
    {
        var a = MakeFull();
        var b = a with
        {
            Model = a.Model with { Model = "gpt-4o" }
        };

        a.ShouldNotBe(b);
    }
}
