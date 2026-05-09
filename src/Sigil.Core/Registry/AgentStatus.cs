using System.Text.Json.Serialization;

namespace Sigil.Core.Registry;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentStatus
{
    Starting,
    Healthy,
    Degraded,
    Offline,
    Draining
}
