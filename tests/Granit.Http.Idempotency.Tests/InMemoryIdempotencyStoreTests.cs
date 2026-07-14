using Granit.Http.Idempotency.Internal;
using Granit.Http.Idempotency.Models;
using Granit.Testing.Fakes;
using Shouldly;
using Xunit;

namespace Granit.Http.Idempotency.Tests;

public sealed class InMemoryIdempotencyStoreTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryIdempotencyStore _store;

    public InMemoryIdempotencyStoreTests() => _store = new InMemoryIdempotencyStore(_timeProvider);

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
    // Contract surface
    // =========================================================================

    [Fact]
    public void IsDistributed_IsFalse() => _store.IsDistributed.ShouldBeFalse();

    [Fact]
    public void BackendName_IsTypeName() => _store.BackendName.ShouldBe(nameof(InMemoryIdempotencyStore));

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

    [Fact]
    public async Task TryAcquireAsync_ParallelRace_ExactlyOneWinner()
    {
        // 16 concurrent acquisitions of the same key — the create-if-absent
        // transition must admit exactly one winner.
        const int Contenders = 16;

        bool[] results = await Task.WhenAll(Enumerable.Range(0, Contenders)
            .Select(_ => Task.Run(() =>
                _store.TryAcquireAsync("contended-key", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None))));

        results.Count(r => r).ShouldBe(1);
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
    // CompleteAsync
    // =========================================================================

    [Fact]
    public async Task CompleteAsync_ExistingKey_UpdatesEntryAndReturnsTrue()
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

        bool written = await _store.CompleteAsync("key-1", completedEntry, TimeSpan.FromHours(24), CancellationToken.None);

        written.ShouldBeTrue();
        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);
        result.ShouldNotBeNull();
        result.State.ShouldBe(IdempotencyState.Completed);
        result.StatusCode.ShouldBe(201);
    }

    [Fact]
    public async Task CompleteAsync_NonExistentKey_DoesNotCreateAndReturnsFalse()
    {
        IdempotencyEntry completedEntry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = "hash-value",
            CreatedAt = DateTimeOffset.UtcNow,
            StatusCode = 200,
        };

        // SET XX semantics: a non-existent key must not be created
        bool written = await _store.CompleteAsync("new-key", completedEntry, TimeSpan.FromHours(24), CancellationToken.None);

        written.ShouldBeFalse();
        IdempotencyEntry? result = await _store.GetAsync("new-key", CancellationToken.None);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_AfterExpiry_ReturnsFalse()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromSeconds(10), CancellationToken.None);

        _timeProvider.Advance(TimeSpan.FromSeconds(11));

        bool written = await _store.CompleteAsync(
            "key-1", CreateEntry(IdempotencyState.Completed), TimeSpan.FromHours(24), CancellationToken.None);

        written.ShouldBeFalse();
        (await _store.GetAsync("key-1", CancellationToken.None)).ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAsync_UpdatesTtl()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromSeconds(10), CancellationToken.None);

        IdempotencyEntry completedEntry = new()
        {
            State = IdempotencyState.Completed,
            PayloadHash = "hash-value",
            CreatedAt = DateTimeOffset.UtcNow,
            StatusCode = 200,
        };

        await _store.CompleteAsync("key-1", completedEntry, TimeSpan.FromHours(1), CancellationToken.None);

        // Advance past original TTL but not the new one
        _timeProvider.Advance(TimeSpan.FromMinutes(30));

        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);
        result.ShouldNotBeNull();
    }

    // =========================================================================
    // TombstoneAsync
    // =========================================================================

    [Fact]
    public async Task TombstoneAsync_ExistingKey_WritesTombstoneAndReturnsTrue()
    {
        await _store.TryAcquireAsync("key-1", CreateEntry(), TimeSpan.FromMinutes(5), CancellationToken.None);

        IdempotencyEntry tombstone = new()
        {
            State = IdempotencyState.Tombstoned,
            PayloadHash = "hash-value",
            CreatedAt = DateTimeOffset.UtcNow,
            StatusCode = 200,
            CompletedAt = DateTimeOffset.UtcNow,
            TombstoneReason = IdempotencyTombstoneReason.ResponseTooLarge,
        };

        bool written = await _store.TombstoneAsync("key-1", tombstone, TimeSpan.FromHours(24), CancellationToken.None);

        written.ShouldBeTrue();
        IdempotencyEntry? result = await _store.GetAsync("key-1", CancellationToken.None);
        result.ShouldNotBeNull();
        result.State.ShouldBe(IdempotencyState.Tombstoned);
        result.TombstoneReason.ShouldBe(IdempotencyTombstoneReason.ResponseTooLarge);
    }

    [Fact]
    public async Task TombstoneAsync_NonExistentKey_DoesNotCreateAndReturnsFalse()
    {
        bool written = await _store.TombstoneAsync(
            "ghost", CreateEntry(IdempotencyState.Tombstoned), TimeSpan.FromHours(24), CancellationToken.None);

        written.ShouldBeFalse();
        (await _store.GetAsync("ghost", CancellationToken.None)).ShouldBeNull();
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
    // Cleanup (opportunistic sweep during TryAcquireAsync)
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
}
