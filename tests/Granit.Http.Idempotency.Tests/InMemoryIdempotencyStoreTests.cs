using Granit.Caching.Internal;
using Granit.Http.Idempotency.Abstractions;
using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class ConditionalCacheIdempotencyStoreTests
{
    private readonly ManualTimeProvider _timeProvider = new(new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly ConditionalCacheIdempotencyStore _store;

    public ConditionalCacheIdempotencyStoreTests()
    {
        InMemoryConditionalCache cache = new(_timeProvider);
        _store = new ConditionalCacheIdempotencyStore(cache, NullLogger<ConditionalCacheIdempotencyStore>.Instance);
    }

    private static IdempotencyEntry CreateEntry(
        IdempotencyState state = IdempotencyState.InProgress,
        string payloadHash = "abc123") =>
        new()
        {
            State = state,
            PayloadHash = payloadHash,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    // =========================================================================
    // TryAcquireAsync
    // =========================================================================

    [Fact]
    public async Task TryAcquireAsync_NewKey_ReturnsTrue()
    {
        bool acquired = await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        acquired.ShouldBeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_DuplicateKey_ReturnsFalse()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        bool acquired = await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        acquired.ShouldBeFalse();
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_BothSucceed()
    {
        bool first = await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);
        bool second = await _store.TryAcquireAsync("key-2", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        first.ShouldBeTrue();
        second.ShouldBeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_ExpiredKey_ReturnsTrue()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromSeconds(10), CancellationToken.None);

        // Advance past TTL
        _timeProvider.Advance(TimeSpan.FromSeconds(11));

        bool acquired = await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        acquired.ShouldBeTrue();
    }

    // =========================================================================
    // GetAsync
    // =========================================================================

    [Fact]
    public async Task GetAsync_ExistingKey_ReturnsEntry()
    {
        IdempotencyEntry entry = CreateEntry(payloadHash: "hash-value");
        await _store.TryAcquireAsync("key-1", entry, TimeSpan.FromMinutes(5), CancellationToken.None);

        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);

        result.ShouldNotBeNull();
        result.PayloadHash.ShouldBe("hash-value");
    }

    [Fact]
    public async Task GetAsync_NonExistentKey_ReturnsNull()
    {
        IdempotencyEntry? result = await _store.GetAsync("nonexistent", CancellationToken.None);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ExpiredKey_ReturnsNull()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromSeconds(10), CancellationToken.None);

        _timeProvider.Advance(TimeSpan.FromSeconds(11));

        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);

        result.ShouldBeNull();

        // Verify entry was removed — a subsequent TryAcquire should succeed
        bool acquired = await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);
        acquired.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAsync_NotYetExpired_ReturnsEntry()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromSeconds(10), CancellationToken.None);

        _timeProvider.Advance(TimeSpan.FromSeconds(9));

        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);

        result.ShouldNotBeNull();
    }

    // =========================================================================
    // SetCompletedAsync
    // =========================================================================

    [Fact]
    public async Task SetCompletedAsync_ExistingKey_UpdatesEntry()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        IdempotencyEntry completedEntry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = "hash-value",
            CreatedAt = DateTimeOffset.UtcNow,
            StatusCode = 201,
            CompletedAt = DateTimeOffset.UtcNow,
        };

        await _store.SetCompletedAsync("key-1", completedEntry, TimeSpan.FromHours(24), CancellationToken.None);

        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);
        result.ShouldNotBeNull();
        result.State.ShouldBe(IdempotencyState.Completed);
        result.StatusCode.ShouldBe(201);
    }

    [Fact]
    public async Task SetCompletedAsync_NonExistentKey_DoesNotCreate()
    {
        IdempotencyEntry completedEntry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = "hash-value",
            CreatedAt = DateTimeOffset.UtcNow,
            StatusCode = 200,
        };

        // SET XX on a non-existent key should not create it
        await _store.SetCompletedAsync("new-key", completedEntry, TimeSpan.FromHours(24), CancellationToken.None);

        IdempotencyEntry? result = await _store.GetAsync("new-key", CancellationToken.None);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task SetCompletedAsync_UpdatesTtl()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromSeconds(10), CancellationToken.None);

        IdempotencyEntry completedEntry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = "hash-value",
            CreatedAt = DateTimeOffset.UtcNow,
            StatusCode = 200,
        };

        await _store.SetCompletedAsync("key-1", completedEntry, TimeSpan.FromHours(1), CancellationToken.None);

        // Advance past original TTL but not the new one
        _timeProvider.Advance(TimeSpan.FromMinutes(30));

        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);
        result.ShouldNotBeNull();
    }

    // =========================================================================
    // DeleteAsync
    // =========================================================================

    [Fact]
    public async Task DeleteAsync_ExistingKey_RemovesEntry()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        await _store.DeleteAsync("key-1", CancellationToken.None);

        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistentKey_DoesNotThrow() =>
        await Should.NotThrowAsync(() => _store.DeleteAsync("nonexistent", CancellationToken.None));

    // =========================================================================
    // Cleanup (called during SetIfAbsentAsync → TryAcquireAsync)
    // =========================================================================

    [Fact]
    public async Task TryAcquireAsync_CleansUpExpiredEntries()
    {
        // Add multiple entries with short TTL
        await _store.TryAcquireAsync("expire-1", CreateEntry(), TimeSpan.FromSeconds(5), CancellationToken.None);
        await _store.TryAcquireAsync("expire-2", CreateEntry(), TimeSpan.FromSeconds(5), CancellationToken.None);
        await _store.TryAcquireAsync("keep-1", CreateEntry(), TimeSpan.FromMinutes(10), CancellationToken.None);

        _timeProvider.Advance(TimeSpan.FromSeconds(6));

        // TryAcquire triggers cleanup
        await _store.TryAcquireAsync("new-key", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        // Expired entries should be removed
        IdempotencyEntry? expired1 = await _store.GetAsync("expire-1", CancellationToken.None);
        IdempotencyEntry? expired2 = await _store.GetAsync("expire-2", CancellationToken.None);
        IdempotencyEntry? kept = await _store.GetAsync("keep-1", CancellationToken.None);

        expired1.ShouldBeNull();
        expired2.ShouldBeNull();
        kept.ShouldNotBeNull();
    }

    // =========================================================================
    // Manual TimeProvider for testing
    // =========================================================================

    private sealed class ManualTimeProvider(DateTimeOffset startTime) : TimeProvider
    {
        private DateTimeOffset _utcNow = startTime;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan delta) => _utcNow += delta;
    }
}
