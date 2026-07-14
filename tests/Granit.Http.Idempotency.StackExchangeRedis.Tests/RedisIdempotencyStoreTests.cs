// =============================================================================
// Tests - RedisIdempotencyStore (mocked IDatabase)
// =============================================================================
// Command-shape and encryption assertions without a live Redis:
//   - acquire uses When.NotExists, complete/tombstone use When.Exists (SET XX)
//   - the bytes handed to Redis are AES ciphertext, never plaintext JSON
//   - InstanceName prefixes the middleware-composed key (no tenant re-namespacing)
// Live atomicity is covered by Granit.Http.Idempotency.StackExchangeRedis.Tests.Integration.
// =============================================================================

using System.Text;
using Granit.Caching;
using Granit.Caching.Options;
using Granit.Http.Idempotency.Models;
using Granit.Http.Idempotency.StackExchangeRedis.Internal;
using Granit.Http.Idempotency.StackExchangeRedis.Options;
using NSubstitute;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.Http.Idempotency.StackExchangeRedis.Tests;

public sealed class RedisIdempotencyStoreTests
{
    private readonly IDatabase _db = Substitute.For<IDatabase>();
    private readonly RedisIdempotencyStore _store;

    public RedisIdempotencyStoreTests()
    {
        IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
        redis.GetDatabase().Returns(_db);

        _store = new RedisIdempotencyStore(
            redis,
            NewAesEncryptor(),
            Microsoft.Extensions.Options.Options.Create(new RedisIdempotencyOptions { InstanceName = "dd:" }));
    }

    private static AesCacheValueEncryptor NewAesEncryptor() =>
        new(Microsoft.Extensions.Options.Options.Create(new CacheEncryptionOptions
        {
            Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
        }));

    private static IdempotencyEntry SampleCompletedEntry() => new()
    {
        State = IdempotencyState.Completed,
        PayloadHash = "hash-value",
        CreatedAt = new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero),
        StatusCode = 200,
        ResponseHeaders = new Dictionary<string, string[]> { ["Content-Type"] = ["application/json"] },
        // A body that would leak PII if stored in plaintext.
        ResponseBody = Encoding.UTF8.GetBytes("""{"ssn":"123-45-6789","email":"jane@example.com"}"""),
        CompletedAt = new DateTimeOffset(2026, 3, 21, 12, 0, 1, TimeSpan.Zero),
    };

    // =========================================================================
    // Contract surface
    // =========================================================================

    [Fact]
    public void IsDistributed_IsTrue() => _store.IsDistributed.ShouldBeTrue();

    [Fact]
    public void BackendName_IsTypeName() => _store.BackendName.ShouldBe(nameof(RedisIdempotencyStore));

    // =========================================================================
    // Command shapes
    // =========================================================================

    [Fact]
    public async Task TryAcquireAsync_UsesSetNotExists_WithTtl()
    {
        RedisKey capturedKey = default;
        When capturedWhen = When.Always;
        TimeSpan? capturedTtl = null;
        _db.StringSetAsync(
                Arg.Do<RedisKey>(k => capturedKey = k),
                Arg.Any<RedisValue>(),
                Arg.Do<TimeSpan?>(t => capturedTtl = t),
                Arg.Do<When>(w => capturedWhen = w))
            .Returns(Task.FromResult(true));

        bool acquired = await _store.TryAcquireAsync(
            "idp:global:user-1:POST:/orders:abc", SampleCompletedEntry(), TimeSpan.FromSeconds(30), CancellationToken.None);

        acquired.ShouldBeTrue();
        capturedWhen.ShouldBe(When.NotExists);
        capturedTtl.ShouldBe(TimeSpan.FromSeconds(30));
        // InstanceName prefix only — the tenant segment already comes from the middleware key.
        ((string?)capturedKey).ShouldBe("dd:idp:global:user-1:POST:/orders:abc");
    }

    [Fact]
    public async Task CompleteAsync_UsesSetExists_AndPropagatesFalse()
    {
        When capturedWhen = When.Always;
        _db.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Do<When>(w => capturedWhen = w))
            .Returns(Task.FromResult(false));

        bool written = await _store.CompleteAsync(
            "key-1", SampleCompletedEntry(), TimeSpan.FromHours(24), CancellationToken.None);

        written.ShouldBeFalse("SET XX returns false when the key expired — must surface to the middleware");
        capturedWhen.ShouldBe(When.Exists);
    }

    [Fact]
    public async Task TombstoneAsync_UsesSetExists()
    {
        When capturedWhen = When.Always;
        _db.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Any<RedisValue>(),
                Arg.Any<TimeSpan?>(),
                Arg.Do<When>(w => capturedWhen = w))
            .Returns(Task.FromResult(true));

        bool written = await _store.TombstoneAsync(
            "key-1", SampleCompletedEntry(), TimeSpan.FromHours(24), CancellationToken.None);

        written.ShouldBeTrue();
        capturedWhen.ShouldBe(When.Exists);
    }

    [Fact]
    public async Task DeleteAsync_DeletesComposedKey()
    {
        RedisKey capturedKey = default;
        _db.KeyDeleteAsync(Arg.Do<RedisKey>(k => capturedKey = k), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        await _store.DeleteAsync("key-1", CancellationToken.None);

        ((string?)capturedKey).ShouldBe("dd:key-1");
    }

    // =========================================================================
    // Encryption at rest
    // =========================================================================

    [Fact]
    public async Task WrittenValue_IsCiphertext_NeverPlaintextJson()
    {
        RedisValue capturedValue = default;
        _db.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Do<RedisValue>(v => capturedValue = v),
                Arg.Any<TimeSpan?>(),
                Arg.Any<When>())
            .Returns(Task.FromResult(true));

        await _store.CompleteAsync("key-1", SampleCompletedEntry(), TimeSpan.FromHours(24), CancellationToken.None);

        byte[] wireBytes = ((byte[]?)capturedValue).ShouldNotBeNull();
        string wireText = Encoding.UTF8.GetString(wireBytes);
        wireText.ShouldNotContain("123-45-6789");
        wireText.ShouldNotContain("jane@example.com");
        wireText.ShouldNotContain("payloadHash", customMessage: "even structural JSON must not be readable");
    }

    [Fact]
    public async Task GetAsync_DecryptsAndRoundTrips()
    {
        RedisValue capturedValue = default;
        _db.StringSetAsync(
                Arg.Any<RedisKey>(),
                Arg.Do<RedisValue>(v => capturedValue = v),
                Arg.Any<TimeSpan?>(),
                Arg.Any<When>())
            .Returns(Task.FromResult(true));

        IdempotencyEntry original = SampleCompletedEntry();
        await _store.CompleteAsync("key-1", original, TimeSpan.FromHours(24), CancellationToken.None);

        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(capturedValue));

        IdempotencyEntry? roundTripped = await _store.GetAsync("key-1", CancellationToken.None);

        roundTripped.ShouldNotBeNull();
        roundTripped.State.ShouldBe(IdempotencyState.Completed);
        roundTripped.StatusCode.ShouldBe(200);
        roundTripped.PayloadHash.ShouldBe(original.PayloadHash);
        roundTripped.ResponseBody.ShouldBe(original.ResponseBody);
        roundTripped.ResponseHeaders.ShouldNotBeNull();
        roundTripped.ResponseHeaders["Content-Type"].ShouldBe(["application/json"]);
    }

    [Fact]
    public async Task GetAsync_MissingKey_ReturnsNull()
    {
        _db.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(RedisValue.Null));

        IdempotencyEntry? result = await _store.GetAsync("ghost", CancellationToken.None);

        result.ShouldBeNull();
    }
}
