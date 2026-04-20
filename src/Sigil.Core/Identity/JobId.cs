namespace Sigil.Core.Identity;

public readonly record struct JobId(string Value)
{
    public override string ToString() => Value;
}
