using Sigil.Core.Protocol;

namespace Sigil.Storage.EfCore.Persistence;

internal sealed class ContextStateRecord
{
    // PK — string form of JobId.Value
    public string JobId { get; set; } = "";

    // Compare-and-set token. Updated atomically with State.
    public string ETag { get; set; } = "";

    // Snapshot state. Stored as jsonb.
    public IReadOnlyDictionary<string, object> State { get; set; }
        = new Dictionary<string, object>();

    // Append-only log of AgentLogEntry. Stored as jsonb.
    public IReadOnlyList<AgentLogEntry> Log { get; set; } = new List<AgentLogEntry>();
}
