using System.Text.Json;
using Granit.Caching;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Granit.Http.Idempotency.Redis;

/// <summary>
/// Redis-backed implementation of <see cref="IIdempotencyStore"/>.
/// Uses atomic SET NX PX / SET XX PX to manage the idempotency state machine.
/// All entries are encrypted with <see cref="ICacheValueEncryptor"/> (AES-256-CBC in production).
/// </summary>
internal sealed partial class RedisIdempotencyStore(
    IConnectionMultiplexer redis,
    ICacheValueEncryptor encryptor,
    ILogger<RedisIdempotencyStore> logger) : IIdempotencyStore
{
    private readonly IDatabase _db = redis.GetDatabase();
    private readonly ICacheValueEncryptor _encryptor = encryptor;

    /// <inheritdoc/>
    public async Task<bool> TryAcquireAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
    {
        RedisValue payload = Serialize(entry);
        bool acquired = await _db.StringSetAsync(key, payload, ttl, When.NotExists).WaitAsync(cancellationToken).ConfigureAwait(false);
        return acquired;
    }

    /// <inheritdoc/>
    public async Task<IdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken)
    {
        RedisValue raw = await _db.StringGetAsync(key).WaitAsync(cancellationToken).ConfigureAwait(false);
        if (raw.IsNullOrEmpty)
        {
            return null;
        }

        return Deserialize(raw);
    }

    /// <inheritdoc/>
    public async Task SetCompletedAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken)
    {
        RedisValue payload = Serialize(entry);
        bool updated = await _db.StringSetAsync(key, payload, ttl, When.Exists).WaitAsync(cancellationToken).ConfigureAwait(false);

        if (!updated)
        {
            LogKeyExpiredBeforeCompletion(key);
        }
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        _db.KeyDeleteAsync(key).WaitAsync(cancellationToken);

    // -------------------------------------------------------------------------
    // Serialization helpers
    // -------------------------------------------------------------------------

    private RedisValue Serialize(IdempotencyEntry entry) =>
        _encryptor.Encrypt(JsonSerializer.SerializeToUtf8Bytes(entry, IdempotencyJsonContext.Default.IdempotencyEntry));

    private IdempotencyEntry? Deserialize(RedisValue raw)
    {
        byte[]? bytes = (byte[]?)raw;
        if (bytes is null)
        {
            return null;
        }

        byte[] decrypted = _encryptor.Decrypt(bytes);
        return JsonSerializer.Deserialize(decrypted, IdempotencyJsonContext.Default.IdempotencyEntry);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "SetCompletedAsync: key {Key} no longer exists (InProgress TTL may have expired before response completed).")]
    private partial void LogKeyExpiredBeforeCompletion(string key);
}
