using Sigil.Core.Registry;

namespace Sigil.Runtime.Tests.Registry;

/// <summary>
/// Deterministic random provider for tests. Either replays a queued sequence of values
/// or delegates to a seeded <see cref="System.Random"/>.
/// </summary>
internal sealed class StubRandomProvider : IRandomProvider
{
    private readonly Queue<int>? _queue;
    private readonly Random? _seeded;

    public StubRandomProvider(IEnumerable<int> values) => _queue = new Queue<int>(values);

    public StubRandomProvider(int seed) => _seeded = new Random(seed);

    public int Next(int maxExclusive)
    {
        if (_queue is not null)
        {
            var raw = _queue.Dequeue();
            return ((raw % maxExclusive) + maxExclusive) % maxExclusive;
        }
        return _seeded!.Next(maxExclusive);
    }
}
