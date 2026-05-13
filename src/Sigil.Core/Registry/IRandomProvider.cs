namespace Sigil.Core.Registry;

/// <summary>
/// Pluggable random source so weighted-selection tests can be deterministic.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Returns a non-negative random integer less than <paramref name="maxExclusive"/>.
    /// </summary>
    /// <param name="maxExclusive">Exclusive upper bound. Must be &gt; 0.</param>
    int Next(int maxExclusive);
}
