using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Caching;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Models;
using Granit.Http.Idempotency.StackExchangeRedis.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Granit.Http.Idempotency.StackExchangeRedis.Internal;

/// <summary>
/// Redis-backed <see cref="IIdempotencyStore"/> implementing the state machine's atomic
/// transitions with single Redis commands: acquire is <c>SET NX PX</c>, complete/tombstone
/// are <c>SET XX PX</c>.
/// </summary>
/// <remarks>
/// <para>
/// WHY NO LUA: every transition the middleware needs is expressible as one conditional
/// <c>SET</c>. Complete/tombstone only require an existence guard — never a state guard —
/// because the terminal state is written exclusively by the request that won
/// <c>TryAcquireAsync</c>, and <c>ExecutionTimeout &lt; InProgressTtl</c> (enforced by
/// <c>IdempotencyOptionsValidator</c>) guarantees the owner writes before its lock can
/// expire. A Lua script inspecting the stored state would decrypt/deserialize inside Redis
/// for no additional correctness, and would break on encrypted payloads anyway.
/// </para>
/// <para>
/// SECURITY: entries are AES-256-GCM encrypted unconditionally before the bytes reach
/// Redis — a replayable entry carries the original response (potential PII/bearer tokens),
/// so the <c>[CacheEncrypted]</c>-style always-on behaviour of the former
/// <c>Granit.Caching</c> path is preserved here without the attribute. With no key
/// configured the <see cref="NullCacheValueEncryptor"/> passes plaintext through —
/// permitted in Development only (see <c>RedisIdempotencyEncryptionStartupValidator</c>).
/// </para>
/// <para>
/// Keys arrive fully tenant/user-namespaced from the middleware; this store only prepends
/// the application-scoped <see cref="RedisIdempotencyOptions.InstanceName"/> (raw
/// <see cref="IDatabase"/> writes bypass any <c>IDistributedCache</c> instance prefix) and
/// MUST NOT re-namespace by tenant.
/// </para>
/// </remarks>
internal sealed class RedisIdempotencyStore(
    IConnectionMultiplexer redis,
    ICacheValueEncryptor encryptor,
    IOptions<RedisIdempotencyOptions> options) : IIdempotencyStore
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IDatabase _db = redis.GetDatabase();
    private readonly string _instanceName = options.Value.InstanceName;

    /// <inheritdoc/>
    public bool IsDistributed => true;

    /// <inheritdoc/>
    public string BackendName => nameof(RedisIdempotencyStore);

    /// <inheritdoc/>
    public async Task<bool> TryAcquireAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken) =>
        await _db.StringSetAsync(Compose(key), Serialize(entry), ttl, When.NotExists)
            .WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IdempotencyEntry?> GetAsync(string key, CancellationToken cancellationToken)
    {
        RedisValue raw = await _db.StringGetAsync(Compose(key))
            .WaitAsync(cancellationToken).ConfigureAwait(false);

        return raw.IsNullOrEmpty ? null : Deserialize(raw);
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken) =>
        await _db.StringSetAsync(Compose(key), Serialize(entry), ttl, When.Exists)
            .WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<bool> TombstoneAsync(string key, IdempotencyEntry entry, TimeSpan ttl, CancellationToken cancellationToken) =>
        await _db.StringSetAsync(Compose(key), Serialize(entry), ttl, When.Exists)
            .WaitAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        await _db.KeyDeleteAsync(Compose(key)).WaitAsync(cancellationToken).ConfigureAwait(false);

    private string Compose(string key) => $"{_instanceName}{key}";

    private RedisValue Serialize(IdempotencyEntry entry) =>
        encryptor.Encrypt(JsonSerializer.SerializeToUtf8Bytes(entry, s_jsonOptions));

    private IdempotencyEntry? Deserialize(RedisValue raw)
    {
        byte[]? bytes = (byte[]?)raw;
        return bytes is null
            ? null
            : JsonSerializer.Deserialize<IdempotencyEntry>(encryptor.Decrypt(bytes), s_jsonOptions);
    }
}
