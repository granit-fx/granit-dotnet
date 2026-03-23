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
    private const string UserIndexPrefix = "bff:user-sessions:";

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

        // Maintain user→sessions index for session listing
        if (!string.IsNullOrEmpty(tokens.UserId))
        {
            await AddToUserIndexAsync(frontendName, tokens.UserId, sessionId, cancellationToken)
                .ConfigureAwait(false);
        }
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

        // Remove from user index if possible
        BffTokenSet? tokens = await GetAsync(frontendName, sessionId, cancellationToken).ConfigureAwait(false);
        if (tokens?.UserId is not null)
        {
            await RemoveFromUserIndexAsync(frontendName, tokens.UserId, sessionId, cancellationToken)
                .ConfigureAwait(false);
        }

        await cache.RemoveAsync(BuildKey(frontendName, sessionId), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetSessionIdsByUserAsync(
        string frontendName, string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frontendName);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        byte[]? bytes = await cache.GetAsync(BuildUserIndexKey(frontendName, userId), cancellationToken)
            .ConfigureAwait(false);

        if (bytes is null or { Length: 0 })
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(bytes, JsonOptions) ?? [];
    }

    private async Task AddToUserIndexAsync(
        string frontendName, string userId, string sessionId, CancellationToken cancellationToken)
    {
        string indexKey = BuildUserIndexKey(frontendName, userId);
        List<string> sessionIds = await GetUserIndexAsync(indexKey, cancellationToken).ConfigureAwait(false);

        if (!sessionIds.Contains(sessionId))
        {
            sessionIds.Add(sessionId);
            await SaveUserIndexAsync(indexKey, sessionIds, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RemoveFromUserIndexAsync(
        string frontendName, string userId, string sessionId, CancellationToken cancellationToken)
    {
        string indexKey = BuildUserIndexKey(frontendName, userId);
        List<string> sessionIds = await GetUserIndexAsync(indexKey, cancellationToken).ConfigureAwait(false);

        if (sessionIds.Remove(sessionId))
        {
            await SaveUserIndexAsync(indexKey, sessionIds, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<List<string>> GetUserIndexAsync(string indexKey, CancellationToken cancellationToken)
    {
        byte[]? bytes = await cache.GetAsync(indexKey, cancellationToken).ConfigureAwait(false);
        if (bytes is null or { Length: 0 })
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(bytes, JsonOptions) ?? [];
    }

    private async Task SaveUserIndexAsync(string indexKey, List<string> sessionIds, CancellationToken cancellationToken)
    {
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(sessionIds, JsonOptions);
        DistributedCacheEntryOptions cacheOptions = new()
        {
            AbsoluteExpiration = clock.Now.Add(options.Value.SessionAbsoluteMaxDuration),
        };

        await cache.SetAsync(indexKey, json, cacheOptions, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildKey(string frontendName, string sessionId) => $"{KeyPrefix}{frontendName}:{sessionId}";
    private static string BuildUserIndexKey(string frontendName, string userId) => $"{UserIndexPrefix}{frontendName}:{userId}";
}
