using System.Text.Json;
using Granit.Bff.Options;
using Granit.Timing;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace Granit.Bff.Internal;

/// <summary>
/// <see cref="IBffTokenStore"/> implementation backed by <see cref="IDistributedCache"/>.
/// Keys follow the pattern <c>bff:session:{frontendName}:{sessionId}</c>.
/// </summary>
internal sealed class DistributedCacheBffTokenStore(
    IDistributedCache cache,
    IOptions<GranitBffOptions> options,
    IClock clock) : IBffTokenStore
{
    private const string KeyPrefix = "bff:session:";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task StoreAsync(string frontendName, string sessionId, BffTokenSet tokens, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(tokens);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(tokens, JsonOptions);
        DistributedCacheEntryOptions cacheOptions = new()
        {
            AbsoluteExpiration = clock.Now.Add(options.Value.SessionDuration),
        };

        await cache.SetAsync(BuildKey(frontendName, sessionId), json, cacheOptions, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<BffTokenSet?> GetAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        byte[]? bytes = await cache.GetAsync(BuildKey(frontendName, sessionId), cancellationToken)
            .ConfigureAwait(false);

        if (bytes is null or { Length: 0 })
        {
            return null;
        }

        return JsonSerializer.Deserialize<BffTokenSet>(bytes, JsonOptions);
    }

    public async Task RemoveAsync(string frontendName, string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await cache.RemoveAsync(BuildKey(frontendName, sessionId), cancellationToken)
            .ConfigureAwait(false);
    }

    private static string BuildKey(string frontendName, string sessionId) => $"{KeyPrefix}{frontendName}:{sessionId}";
}
