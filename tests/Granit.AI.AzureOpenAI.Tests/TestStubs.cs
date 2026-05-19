using Granit.AI.AzureOpenAI.Internal;
using Granit.AI.AzureOpenAI.Options;
using Granit.AI.Tenancy;
using Granit.Settings.Definitions;
using Granit.Settings.Providers;
using Granit.Settings.Values;
using Microsoft.Extensions.Options;

namespace Granit.AI.AzureOpenAI.Tests;

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
    public HttpClient CreateClient(string name) => new();
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
    public static AzureOpenAIProviderOptions DefaultOptions() => new()
    {
        Endpoint = "https://my-resource.openai.azure.com",
        ApiKey = "test-key",
        DefaultDeployment = "gpt-4o",
        DefaultEmbeddingDeployment = "text-embedding-3-small",
    };

    public static SettingDefinitionManager BuildDefinitionManager() =>
        new(new ISettingDefinitionProvider[] { new AISettingDefinitionProvider() });

    public static (AzureOpenAIProviderFactory Factory,
                   TestSettingValueProvider Tenant,
                   TestSettingValueProvider Global,
                   TestOptionsMonitor<AzureOpenAIProviderOptions> Monitor)
        BuildFactory(AzureOpenAIProviderOptions? options = null)
    {
        var monitor = new TestOptionsMonitor<AzureOpenAIProviderOptions>(options ?? DefaultOptions());
        TestSettingValueProvider tenant = new(TenantSettingValueProvider.ProviderName);
        TestSettingValueProvider global = new(GlobalSettingValueProvider.ProviderName);
        SettingDefinitionManager definitions = BuildDefinitionManager();
        AzureOpenAICredentialResolver resolver = new(definitions, [tenant, global], monitor);
        AzureOpenAIClientCache cache = new(new TestHttpClientFactory(), Microsoft.Extensions.Options.Options.Create(monitor.CurrentValue));
        AzureOpenAIProviderFactory factory = new(monitor, resolver, cache);
        return (factory, tenant, global, monitor);
    }
}
