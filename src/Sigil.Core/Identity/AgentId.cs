namespace Sigil.Core.Identity;

public readonly record struct AgentId(string Value)
{
    public override string ToString() => Value;
}
