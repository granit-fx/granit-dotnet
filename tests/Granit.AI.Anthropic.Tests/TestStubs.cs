using Granit.AI.Anthropic.Options;
using Microsoft.Extensions.Options;

namespace Granit.AI.Anthropic.Tests;

/// <summary>
/// In-memory <see cref="IOptionsMonitor{TOptions}"/> for tests that need to drive
/// option-change callbacks deterministically.
/// </summary>
internal sealed class TestOptionsMonitor<T>(T initial) : IOptionsMonitor<T>
    where T : class
{
    private T _current = initial;
    private readonly List<Action<T, string?>> _listeners = [];

    public T CurrentValue => _current;

    public T Get(string? name) => _current;

    public IDisposable OnChange(Action<T, string?> listener)
    {
        lock (_listeners)
        {
            _listeners.Add(listener);
        }
        return new Subscription(this, listener);
    }

    public void Set(T next)
    {
        _current = next;
        Action<T, string?>[] snapshot;
        lock (_listeners)
        {
            snapshot = [.. _listeners];
        }
        foreach (Action<T, string?> listener in snapshot)
        {
            listener(next, null);
        }
    }

    private sealed class Subscription(TestOptionsMonitor<T> owner, Action<T, string?> listener) : IDisposable
    {
        public void Dispose()
        {
            lock (owner._listeners)
            {
                owner._listeners.Remove(listener);
            }
        }
    }
}

/// <summary>
/// Minimal <see cref="IHttpClientFactory"/> that hands out a fresh <see cref="HttpClient"/>
/// per call — sufficient for factory construction; no network traffic is exercised in unit tests.
/// </summary>
internal sealed class TestHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}

internal static class TestFixtures
{
    public static AnthropicProviderOptions DefaultOptions() => new()
    {
        ApiKey = "sk-ant-test-key",
        DefaultModel = "claude-sonnet-4-6",
    };
}
