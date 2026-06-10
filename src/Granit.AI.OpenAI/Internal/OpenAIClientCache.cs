using System.ClientModel;
using System.ClientModel.Primitives;
using Granit.AI.OpenAI.Options;
using Granit.AI.Tenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Granit.AI.OpenAI.Internal;

/// <summary>
/// Singleton cache of <see cref="OpenAIClient"/> instances keyed by
/// SHA-256(<c>ApiKey</c>) + <c>Endpoint</c>.
/// </summary>
internal sealed class OpenAIClientCache(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenAIProviderOptions> options) : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions
    {
        SizeLimit = 1_000,
    });

    /// <summary>Sliding expiration applied to every cached SDK client.</summary>
    internal static readonly TimeSpan SlidingExpiration = TimeSpan.FromSeconds(90);

    /// <summary>Fetches (or creates and caches) the SDK client for the given credentials.</summary>
    public OpenAIClient GetOrCreate(string apiKey, string? endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var key = AICacheKey.ForApiKey(apiKey, endpoint);
        if (_cache.TryGetValue(key, out object? cached) && cached is OpenAIClient existing)
        {
            return existing;
        }

        OpenAIClient client = BuildClient(apiKey, endpoint);

        MemoryCacheEntryOptions entryOptions = new()
        {
            SlidingExpiration = SlidingExpiration,
            Size = 1,
        };

        return _cache.Set(key, client, entryOptions);
    }

    /// <summary>Clears every cached client.</summary>
    public void InvalidateAll() => _cache.Clear();

    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();

    private OpenAIClient BuildClient(string apiKey, string? endpoint)
    {
        OpenAIProviderOptions opts = options.Value;
        HttpClient http = httpClientFactory.CreateClient(OpenAIProviderFactory.HttpClientName);

        var credential = new ApiKeyCredential(apiKey);
        OpenAIClientOptions clientOptions = new()
        {
            Transport = new HttpClientPipelineTransport(http),
            NetworkTimeout = opts.Timeout,
            RetryPolicy = new ClientRetryPolicy(opts.MaxRetries),
        };

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            clientOptions.Endpoint = new Uri(endpoint);
        }

        return new OpenAIClient(credential, clientOptions);
    }
}
