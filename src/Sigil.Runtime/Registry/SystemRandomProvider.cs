using Sigil.Core.Registry;

namespace Sigil.Runtime.Registry;

/// <summary>
/// Default <see cref="IRandomProvider"/> backed by <see cref="System.Random.Shared"/>.
/// </summary>
public sealed class SystemRandomProvider : IRandomProvider
{
    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
