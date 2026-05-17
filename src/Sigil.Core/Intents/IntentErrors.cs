namespace Sigil.Core.Intents;

/// <summary>
/// Stable string error codes returned by <see cref="IIntentDispatcher"/>.
/// Consumers (endpoints, logs, tests) match on these values.
/// </summary>
public static class IntentErrors
{
    public const string NoAgentForSkill = "no-agent-for-skill";
    public const string ValidationRejected = "validation-rejected";
}
