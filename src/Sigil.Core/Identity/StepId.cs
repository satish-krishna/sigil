namespace Sigil.Core.Identity;

public readonly record struct StepId(string Value)
{
    public override string ToString() => Value;
}
