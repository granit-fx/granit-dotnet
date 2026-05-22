using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.AI.Tenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Granit.Settings.Values;
using Microsoft.Extensions.Options;

namespace Granit.AI.Ollama.Tests;

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

internal sealed class TestHttpClientFactory : IHttpClientFactory
{
    public List<string> RequestedNames { get; } = [];

    public HttpClient CreateClient(string name)
    {
        RequestedNames.Add(name);
        return new HttpClient();
    }
}

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
    public static OllamaProviderOptions DefaultOptions() => new()
    {
        Endpoint = "http://localhost:11434",
        DefaultModel = "llama3.2",
    };

    public static SettingDefinitionManager BuildDefinitionManager() =>
        new(new ISettingDefinitionProvider[] { new AISettingDefinitionProvider() });

    public static (OllamaProviderFactory Factory,
                   TestSettingValueProvider Tenant,
                   TestSettingValueProvider Global,
                   TestOptionsMonitor<OllamaProviderOptions> Monitor)
        BuildFactory(OllamaProviderOptions? options = null)
    {
        (OllamaProviderFactory factory, TestSettingValueProvider tenant, TestSettingValueProvider global,
            TestOptionsMonitor<OllamaProviderOptions> monitor, _) = BuildFactoryWithHttp(options);
        return (factory, tenant, global, monitor);
    }

    public static (OllamaProviderFactory Factory,
                   TestSettingValueProvider Tenant,
                   TestSettingValueProvider Global,
                   TestOptionsMonitor<OllamaProviderOptions> Monitor,
                   TestHttpClientFactory HttpFactory)
        BuildFactoryWithHttp(OllamaProviderOptions? options = null)
    {
        var monitor = new TestOptionsMonitor<OllamaProviderOptions>(options ?? DefaultOptions());
        TestSettingValueProvider tenant = new(TenantSettingValueProvider.ProviderName);
        TestSettingValueProvider global = new(GlobalSettingValueProvider.ProviderName);
        SettingDefinitionManager definitions = BuildDefinitionManager();
        OllamaCredentialResolver resolver = new(definitions, [tenant, global], monitor);
        TestHttpClientFactory httpFactory = new();
        OllamaClientCache cache = new(httpFactory, Microsoft.Extensions.Options.Options.Create(monitor.CurrentValue));
        OllamaProviderFactory factory = new(monitor, resolver, cache, TimeProvider.System);
        return (factory, tenant, global, monitor, httpFactory);
    }
}
