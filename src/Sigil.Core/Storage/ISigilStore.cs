namespace Sigil.Core.Storage;

public interface ISigilStore
{
    IAgentRegistrationStore Agents { get; }
    IJobStore Jobs { get; }
    IContextStore Contexts { get; }
    ICheckpointStore Checkpoints { get; }
    IAuditStore Audit { get; }
}
