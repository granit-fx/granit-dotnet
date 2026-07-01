using Granit.AI.Anthropic.Internal;
using Granit.AI.Anthropic.Options;
using Granit.AI.Tenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Granit.Settings.Values;
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

/// <summary>
/// In-memory <see cref="ISettingValueProvider"/> for tests. Holds (settingName → value) and
/// returns a synthetic <see cref="SettingValue"/> with the configured <c>Name</c>.
/// </summary>
internal sealed class TestSettingValueProvider(string name) : ISettingValueProvider
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.Ordinal);

    public string Name { get; } = name;

    public int Order => 0;

    public void Set(string settingName, string? value) => _values[settingName] = value;

    public Task<SettingValue?> GetOrNullAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        if (!_values.TryGetValue(definition.Name, out string? value) || value is null)
        {
            return Task.FromResult<SettingValue?>(null);
        }
        return Task.FromResult<SettingValue?>(new SettingValue(definition.Name, Name, ProviderKey: null, value));
    }

    public Task SetAsync(SettingDefinition definition, string? value, CancellationToken cancellationToken = default)
    {
        _values[definition.Name] = value;
        return Task.CompletedTask;
    }

    public Task ClearAsync(SettingDefinition definition, CancellationToken cancellationToken = default)
    {
        _values.Remove(definition.Name);
        return Task.CompletedTask;
    }
}

internal static class TestFixtures
{
    public static AnthropicProviderOptions DefaultOptions() => new()
    {
        ApiKey = "sk-ant-test-key",
        DefaultModel = "claude-sonnet-4-6",
    };

    /// <summary>Builds a real <see cref="SettingDefinitionRegistry"/> seeded with the AI settings.</summary>
    public static SettingDefinitionRegistry BuildDefinitionManager() =>
        new(new ISettingDefinitionProvider[] { new AISettingDefinitionProvider() });

    /// <summary>Builds a credential resolver wired with in-memory tenant/global providers.</summary>
    public static (AnthropicCredentialResolver Resolver,
                   TestSettingValueProvider Tenant,
                   TestSettingValueProvider Global,
                   TestOptionsMonitor<AnthropicProviderOptions> Monitor)
        BuildResolver(AnthropicProviderOptions? options = null)
    {
        var monitor = new TestOptionsMonitor<AnthropicProviderOptions>(options ?? DefaultOptions());
        TestSettingValueProvider tenant = new(TenantSettingValueProvider.ProviderName);
        TestSettingValueProvider global = new(GlobalSettingValueProvider.ProviderName);
        SettingDefinitionRegistry definitions = BuildDefinitionManager();
        AnthropicCredentialResolver resolver = new(definitions, [tenant, global], monitor);
        return (resolver, tenant, global, monitor);
    }

    /// <summary>Builds a fully-wired Anthropic factory (resolver + cache).</summary>
    public static (AnthropicProviderFactory Factory,
                   TestSettingValueProvider Tenant,
                   TestSettingValueProvider Global,
                   TestOptionsMonitor<AnthropicProviderOptions> Monitor)
        BuildFactory(AnthropicProviderOptions? options = null)
    {
        (AnthropicCredentialResolver resolver, TestSettingValueProvider tenant, TestSettingValueProvider global, TestOptionsMonitor<AnthropicProviderOptions> monitor) = BuildResolver(options);
        AnthropicClientCache cache = new(new TestHttpClientFactory(), Microsoft.Extensions.Options.Options.Create(monitor.CurrentValue));
        AnthropicProviderFactory factory = new(monitor, resolver, cache);
        return (factory, tenant, global, monitor);
    }
}
