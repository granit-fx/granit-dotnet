using Granit.AI.Ollama.Options;
using Granit.AI.Tenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace Granit.AI.Ollama.Internal;

/// <summary>
/// Singleton cache of <see cref="OllamaApiClient"/> instances keyed by Endpoint+Model+Scope.
/// </summary>
/// <remarks>
/// <para>
/// Unlike API-keyed providers, Ollama's <see cref="OllamaApiClient"/> binds a single model per
/// instance via constructor argument. Cache keys therefore include the model identifier so the
/// same Endpoint can serve multiple models concurrently without thrashing.
/// </para>
/// <para>
/// The credential scope is also part of the key: Host-scoped credentials use a permissive
/// connect-time policy that allows private IPs, while Tenant/Workspace/Global credentials use
/// the strict tenant policy. Sharing a single cached client across scopes would let a Tenant
/// endpoint inherit the permissive policy and defeat the DNS-rebinding defence.
/// </para>
/// </remarks>
internal sealed class OllamaClientCache(
    IHttpClientFactory httpClientFactory,
    IOptions<OllamaProviderOptions> options) : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions
    {
        SizeLimit = 1_000,
    });

    internal static readonly TimeSpan SlidingExpiration = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Fetches (or creates and caches) an SDK client bound to the given endpoint + model,
    /// routed through the named <see cref="HttpClient"/> that matches the credential scope.
    /// </summary>
    public OllamaApiClient GetOrCreate(string endpoint, string model, AIProviderCredentialScope scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        string key = $"{endpoint}|{model}|{scope.ToString()}";
        if (_cache.TryGetValue(key, out object? cached) && cached is OllamaApiClient existing)
        {
            return existing;
        }

        OllamaApiClient client = BuildClient(endpoint, model, scope);
        MemoryCacheEntryOptions entryOptions = new()
        {
            SlidingExpiration = SlidingExpiration,
            Size = 1,
        };
        entryOptions.PostEvictionCallbacks.Add(new PostEvictionCallbackRegistration
        {
            EvictionCallback = static (_, value, _, _) =>
                (value as IDisposable)?.Dispose(),
        });

        return _cache.Set(key, client, entryOptions);
    }

    public void InvalidateAll() => _cache.Clear();

    public void Dispose() => _cache.Dispose();

    private OllamaApiClient BuildClient(string endpoint, string model, AIProviderCredentialScope scope)
    {
        OllamaProviderOptions opts = options.Value;
        string clientName = scope == AIProviderCredentialScope.Host
            ? OllamaProviderFactory.HostHttpClientName
            : OllamaProviderFactory.TenantHttpClientName;
        HttpClient http = httpClientFactory.CreateClient(clientName);
        http.BaseAddress = new Uri(endpoint);
        http.Timeout = opts.Timeout;
        return new OllamaApiClient(http, model);
    }
}
