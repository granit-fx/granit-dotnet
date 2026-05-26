using Anthropic;
using Granit.AI.Anthropic.Options;
using Granit.AI.Tenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Granit.AI.Anthropic.Internal;

/// <summary>
/// Singleton cache of <see cref="AnthropicClient"/> instances keyed by SHA-256(<c>ApiKey</c>).
/// </summary>
/// <remarks>
/// <para>
/// Maintains one SDK client per distinct API key so that requests for the same credential
/// share a connection-pooled <see cref="HttpClient"/>. Entries are evicted by
/// <see cref="MemoryCacheEntryOptions.SlidingExpiration"/> (default 90s) so a rotated key
/// stops serving traffic quickly even if no <c>SettingChangedEvent</c> handler runs.
/// </para>
/// <para>
/// Cache keys are typed <see cref="AICacheKey"/> records carrying the SHA-256 hash of the API
/// key, never the plaintext, so cache keys cannot be coerced back into credential material.
/// Post-eviction callbacks dispose the
/// <see cref="AnthropicClient"/>; the <see cref="HttpClient"/> is owned by
/// <see cref="IHttpClientFactory"/>.
/// </para>
/// </remarks>
internal sealed class AnthropicClientCache(
    IHttpClientFactory httpClientFactory,
    IOptions<AnthropicProviderOptions> options) : IDisposable
{
    private readonly MemoryCache _cache = new(new MemoryCacheOptions
    {
        SizeLimit = 1_000,
    });

    /// <summary>Sliding expiration applied to every cached SDK client.</summary>
    internal static readonly TimeSpan SlidingExpiration = TimeSpan.FromSeconds(90);

    /// <summary>Fetches (or creates and caches) the SDK client for the given API key.</summary>
    public AnthropicClient GetOrCreate(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var key = AICacheKey.ForApiKey(apiKey);
        if (_cache.TryGetValue(key, out object? cached) && cached is AnthropicClient existing)
        {
            return existing;
        }

        AnthropicClient client = BuildClient(apiKey);

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

    /// <summary>Removes the cached client for the given API key (if any).</summary>
    public void Invalidate(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        _cache.Remove(AICacheKey.ForApiKey(apiKey));
    }

    /// <summary>Wipes every cached client — used when the rotation event cannot identify the affected key.</summary>
    public void InvalidateAll()
    {
        if (_cache is MemoryCache mc)
        {
            mc.Clear();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _cache.Dispose();

    private AnthropicClient BuildClient(string apiKey)
    {
        AnthropicProviderOptions opts = options.Value;
        HttpClient http = httpClientFactory.CreateClient(AnthropicProviderFactory.HttpClientName);

        return new AnthropicClient
        {
            ApiKey = apiKey,
            HttpClient = http,
            Timeout = opts.Timeout,
            MaxRetries = opts.MaxRetries,
        };
    }
}
