namespace Sigil.Core.Registry;

/// <summary>
/// Stable string error codes returned by <see cref="IAgentRegistry"/>.
/// Consumers (endpoints, logs, tests) match on these values.
/// </summary>
public static class RegistryErrors
{
    public const string AgentNotFound = "agent-not-found";
    public const string InvalidStatusTransition = "invalid-status-transition";
    public const string InvalidRoutingWeight = "invalid-routing-weight";
    public const string SkillNameRequired = "skill-name-required";
}
