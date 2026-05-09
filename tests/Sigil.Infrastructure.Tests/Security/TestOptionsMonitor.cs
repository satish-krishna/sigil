using Microsoft.Extensions.Options;

namespace Sigil.Infrastructure.Tests.Security;

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
    where T : class
{
    public TestOptionsMonitor(T initialValue)
    {
        CurrentValue = initialValue;
    }

    public T CurrentValue { get; set; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
