namespace Sigil.Core.Identity;

public readonly record struct ETag(string Value)
{
    public override string ToString() => Value;
}
