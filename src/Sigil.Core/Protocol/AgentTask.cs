using Sigil.Core.Identity;

namespace Sigil.Core.Protocol;

public sealed record AgentTask
{
    public JobId JobId { get; init; }
    public StepId StepId { get; init; }
    public required string SkillName { get; init; }
    public string Input { get; init; } = "";
    public IReadOnlyList<string> AvailableTools { get; init; } = [];

    public bool Equals(AgentTask? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return JobId == other.JobId
            && StepId == other.StepId
            && SkillName == other.SkillName
            && Input == other.Input
            && AvailableTools.SequenceEqual(other.AvailableTools);
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(JobId);
        hash.Add(StepId);
        hash.Add(SkillName);
        hash.Add(Input);
        foreach (var tool in AvailableTools) hash.Add(tool);
        return hash.ToHashCode();
    }
}
