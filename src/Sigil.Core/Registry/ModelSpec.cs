namespace Sigil.Core.Registry;

public sealed record ModelSpec
{
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public Sampling Sampling { get; init; } = new();
}

public sealed record Sampling
{
    public double? Temperature { get; init; }
    public double? TopP { get; init; }
    public int? MaxOutputTokens { get; init; }
}
