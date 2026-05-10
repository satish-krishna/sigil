using Sigil.Core.Storage;

namespace Sigil.Storage.EfCore;

public sealed class EfSigilStore : ISigilStore
{
    public EfSigilStore(
        IAgentRegistrationStore agents,
        IJobStore jobs,
        IContextStore contexts,
        ICheckpointStore checkpoints,
        IAuditStore audit)
    {
        Agents = agents;
        Jobs = jobs;
        Contexts = contexts;
        Checkpoints = checkpoints;
        Audit = audit;
    }

    public IAgentRegistrationStore Agents { get; }
    public IJobStore Jobs { get; }
    public IContextStore Contexts { get; }
    public ICheckpointStore Checkpoints { get; }
    public IAuditStore Audit { get; }
}
