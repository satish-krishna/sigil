using Sigil.Core.Identity;
using Sigil.Core.Registry;

namespace Sigil.Runtime.Tests.Registry;

internal static class TestAgents
{
    public static AgentRegistration Make(
        string id,
        AgentStatus status = AgentStatus.Starting,
        int routingWeight = 100,
        string skillName = "echo")
        => new()
        {
            AgentId = new AgentId(id),
            Name = id,
            Domain = "test",
            EndpointUrl = $"https://{id}.internal",
            RoutingWeight = routingWeight,
            Status = status,
            Model = new ModelSpec { Provider = "openai", Model = "gpt-4o-mini" },
            Skills =
            [
                new Skill { Name = skillName, Description = "test skill" }
            ]
        };
}
