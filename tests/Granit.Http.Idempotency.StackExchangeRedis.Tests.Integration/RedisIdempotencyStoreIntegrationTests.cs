// =============================================================================
// Tests - RedisIdempotencyStore against a real Redis (Testcontainers)
// =============================================================================
// Store-level atomic transition coverage:
//   - acquire race: 16 parallel TryAcquire → exactly one winner (SET NX PX)
//   - complete-after-expiry returns false (SET XX on an expired key)
//   - tombstone write + read-back
//   - encrypted-at-rest: the raw Redis value is AES ciphertext, never JSON
// =============================================================================

using System.Text;
using Granit.Caching;
using Granit.Caching.Options;
using Granit.Http.Idempotency.Models;
using Granit.Http.Idempotency.StackExchangeRedis.Internal;
using Granit.Http.Idempotency.StackExchangeRedis.Options;
using Shouldly;
using StackExchange.Redis;
using Xunit;

namespace Granit.Http.Idempotency.StackExchangeRedis.Tests.Integration;

public sealed class RedisIdempotencyStoreIntegrationTests(RedisContainerFixture fixture)
    : IClassFixture<RedisContainerFixture>, IAsyncLifetime
{
    private const string InstanceName = "it:idp:";

    private ConnectionMultiplexer _redis = null!;
    private RedisIdempotencyStore _store = null!;

    public async ValueTask InitializeAsync()
    {
        _redis = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        _store = new RedisIdempotencyStore(
            _redis,
            new AesCacheValueEncryptor(Microsoft.Extensions.Options.Options.Create(new CacheEncryptionOptions
            {
                Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            })),
            Microsoft.Extensions.Options.Options.Create(new RedisIdempotencyOptions { InstanceName = InstanceName }));
    }

    public ValueTask DisposeAsync()
    {
        _redis.Dispose();
        return ValueTask.CompletedTask;
    }

    private static IdempotencyEntry InProgressEntry() => new()
    {
        State = IdempotencyState.InProgress,
        PayloadHash = "hash-abc",
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static IdempotencyEntry CompletedEntry() => new()
    {
        State = IdempotencyState.Completed,
        PayloadHash = "hash-abc",
        CreatedAt = DateTimeOffset.UtcNow,
        StatusCode = 201,
        ResponseHeaders = new Dictionary<string, string[]> { ["Content-Type"] = ["application/json"] },
        ResponseBody = Encoding.UTF8.GetBytes("""{"ssn":"123-45-6789","email":"jane@example.com"}"""),
        CompletedAt = DateTimeOffset.UtcNow,
    };

    private static string NewKey() => $"idp:global:user-1:POST:/orders:{Guid.NewGuid():N}";

    // =========================================================================
    // Acquire race — SET NX PX atomicity
    // =========================================================================

    [Fact]
    public async Task TryAcquireAsync_16Parallel_ExactlyOneWinner()
    {
        string key = NewKey();

        bool[] results = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => _store.TryAcquireAsync(
                key, InProgressEntry(), TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken)));

        results.Count(r => r).ShouldBe(1);
    }

    // =========================================================================
    // Complete / tombstone transitions — SET XX PX
    // =========================================================================

    [Fact]
    public async Task CompleteAsync_AfterAcquire_RoundTrips()
    {
        string key = NewKey();
        await _store.TryAcquireAsync(key, InProgressEntry(), TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        bool written = await _store.CompleteAsync(key, CompletedEntry(), TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        written.ShouldBeTrue();
        IdempotencyEntry? readBack = await _store.GetAsync(key, TestContext.Current.CancellationToken);
        readBack.ShouldNotBeNull();
        readBack.State.ShouldBe(IdempotencyState.Completed);
        readBack.StatusCode.ShouldBe(201);
        readBack.ResponseBody.ShouldBe(CompletedEntry().ResponseBody);
    }

    [Fact]
    public async Task CompleteAsync_AfterExpiry_ReturnsFalse_AndDoesNotCreate()
    {
        string key = NewKey();
        await _store.TryAcquireAsync(key, InProgressEntry(), TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // Let the InProgress lock expire inside Redis.
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

        bool written = await _store.CompleteAsync(key, CompletedEntry(), TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        written.ShouldBeFalse("SET XX must not resurrect an expired key");
        (await _store.GetAsync(key, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task TombstoneAsync_AfterAcquire_ReplaysTombstone()
    {
        string key = NewKey();
        await _store.TryAcquireAsync(key, InProgressEntry(), TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        IdempotencyEntry tombstone = new()
        {
            State = IdempotencyState.Tombstoned,
            PayloadHash = "hash-abc",
            CreatedAt = DateTimeOffset.UtcNow,
            StatusCode = 200,
            CompletedAt = DateTimeOffset.UtcNow,
            TombstoneReason = IdempotencyTombstoneReason.ResponseTooLarge,
        };

        bool written = await _store.TombstoneAsync(key, tombstone, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        written.ShouldBeTrue();
        IdempotencyEntry? readBack = await _store.GetAsync(key, TestContext.Current.CancellationToken);
        readBack.ShouldNotBeNull();
        readBack.State.ShouldBe(IdempotencyState.Tombstoned);
        readBack.TombstoneReason.ShouldBe(IdempotencyTombstoneReason.ResponseTooLarge);
        readBack.ResponseBody.ShouldBeNull();
    }

    [Fact]
    public async Task TombstoneAsync_WithoutAcquire_ReturnsFalse()
    {
        string key = NewKey();

        bool written = await _store.TombstoneAsync(
            key,
            new IdempotencyEntry
            {
                State = IdempotencyState.Tombstoned,
                PayloadHash = "hash-abc",
                CreatedAt = DateTimeOffset.UtcNow,
                TombstoneReason = IdempotencyTombstoneReason.ResponseTooLarge,
            },
            TimeSpan.FromMinutes(5),
            TestContext.Current.CancellationToken);

        written.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ReleasesTheLock()
    {
        string key = NewKey();
        await _store.TryAcquireAsync(key, InProgressEntry(), TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        await _store.DeleteAsync(key, TestContext.Current.CancellationToken);

        (await _store.GetAsync(key, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await _store.TryAcquireAsync(key, InProgressEntry(), TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken))
            .ShouldBeTrue("the key must be re-acquirable after delete");
    }

    // =========================================================================
    // Encrypted at rest
    // =========================================================================

    [Fact]
    public async Task RawRedisValue_IsNotPlaintextJson()
    {
        string key = NewKey();
        await _store.TryAcquireAsync(key, InProgressEntry(), TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        await _store.CompleteAsync(key, CompletedEntry(), TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        RedisValue raw = await _redis.GetDatabase().StringGetAsync($"{InstanceName}{key}");

        raw.IsNullOrEmpty.ShouldBeFalse();
        string rawText = Encoding.UTF8.GetString(((byte[]?)raw)!);
        rawText.ShouldNotContain("123-45-6789");
        rawText.ShouldNotContain("jane@example.com");
        rawText.ShouldNotContain("payloadHash", customMessage: "even structural JSON must not be readable at rest");
    }
}
