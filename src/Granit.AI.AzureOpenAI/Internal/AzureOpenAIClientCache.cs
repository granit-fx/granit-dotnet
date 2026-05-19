using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Azure.Identity;
using Granit.AI.AzureOpenAI.Options;
using Granit.AI.Tenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Granit.AI.AzureOpenAI.Internal;

/// <summary>
/// Singleton cache of <see cref="AzureOpenAIClient"/> instances keyed by
/// SHA-256(<c>ApiKey</c>) + <c>Endpoint</c>, or by Managed Identity + <c>Endpoint</c>.
/// </summary>
internal sealed class AzureOpenAIClientCache(
    IHttpClientFactory httpClientFactory,
    IOptions<AzureOpenAIProviderOptions> options) : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions
    {
        SizeLimit = 1_000,
    });

    /// <summary>Sliding expiration applied to every cached SDK client.</summary>
    internal static readonly TimeSpan SlidingExpiration = TimeSpan.FromSeconds(90);

    /// <summary>Fetches (or creates and caches) the API-key SDK client.</summary>
    public AzureOpenAIClient GetOrCreate(string apiKey, string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var key = AICacheKey.ForApiKey(apiKey, endpoint);
        return GetOrCreateCore(key, () => BuildApiKeyClient(apiKey, endpoint));
    }

    /// <summary>Fetches (or creates and caches) the Managed Identity SDK client.</summary>
    public AzureOpenAIClient GetOrCreateManagedIdentity(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var key = AICacheKey.ForManagedIdentity(endpoint);
        return GetOrCreateCore(key, () => BuildManagedIdentityClient(endpoint));
    }

    private AzureOpenAIClient GetOrCreateCore(AICacheKey key, Func<AzureOpenAIClient> factory)
    {
        if (_cache.TryGetValue(key, out object? cached) && cached is AzureOpenAIClient existing)
        {
            return existing;
        }

        AzureOpenAIClient client = factory();
        MemoryCacheEntryOptions entryOptions = new()
        {
            SlidingExpiration = SlidingExpiration,
            Size = 1,
        };
        return _cache.Set(key, client, entryOptions);
    }

    public void InvalidateAll() => _cache.Clear();

    public void Dispose() => _cache.Dispose();

    private AzureOpenAIClient BuildApiKeyClient(string apiKey, string endpoint) =>
        new(new Uri(endpoint), new ApiKeyCredential(apiKey), BuildClientOptions());

    private AzureOpenAIClient BuildManagedIdentityClient(string endpoint) =>
        new(new Uri(endpoint), new DefaultAzureCredential(), BuildClientOptions());

    private AzureOpenAIClientOptions BuildClientOptions()
    {
        AzureOpenAIProviderOptions opts = options.Value;
        HttpClient http = httpClientFactory.CreateClient(AzureOpenAIProviderFactory.HttpClientName);

        return new AzureOpenAIClientOptions
        {
            Transport = new HttpClientPipelineTransport(http),
            NetworkTimeout = opts.Timeout,
            RetryPolicy = new ClientRetryPolicy(opts.MaxRetries),
        };
    }
}
