using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Identity.Federated.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

public sealed class EfCoreUserCacheStoreTests
{
    private static readonly IUserLookupHasher Hasher = CreateHasher();

    private static IUserLookupHasher CreateHasher()
    {
        // Deterministic stub — mirrors the real HmacUserLookupHasher's public contract
        // (null in → null out, otherwise a stable lower-case token). Tests don't need
        // HMAC semantics, just something that round-trips equality for exact-match.
        IUserLookupHasher hasher = Substitute.For<IUserLookupHasher>();
        hasher.ComputeEmailHash(Arg.Any<string?>())
            .Returns(ci => ci.Arg<string?>() is { Length: > 0 } email
                ? "hash:" + email.Trim().ToLowerInvariant()
                : null);
        return hasher;
    }

    private static EfCoreUserCacheStore<TestDbContext> CreateStore(TestDbContext context) =>
        new(context, Hasher);

    private static TestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static FederatedIdentity CreateEntry(
        string externalUserId = "user-1",
        Guid? tenantId = null,
        string? username = "jdoe",
        string? email = "jdoe@test.com",
        string? firstName = "John",
        string? lastName = "Doe") => new()
        {
            Id = Guid.NewGuid(),
            ExternalUserId = externalUserId,
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Enabled = true,
            LastSyncedAt = DateTimeOffset.UtcNow,
            TenantId = tenantId
        };

    [Fact]
    public async Task FindByExternalIdAsync_ReturnsNull_WhenNotFound()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        FederatedIdentity? result = await store.FindByExternalIdAsync(
            "nonexistent", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewEntry()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);
        FederatedIdentity entry = CreateEntry();

        await store.UpsertAsync(entry, TestContext.Current.CancellationToken);

        FederatedIdentity? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);
        found.ShouldNotBeNull();
        found.Username.ShouldBe("jdoe");
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingEntry()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);
        FederatedIdentity entry = CreateEntry();
        await store.UpsertAsync(entry, TestContext.Current.CancellationToken);

        FederatedIdentity updated = CreateEntry(username: "jdoe-updated", email: "updated@test.com");
        await store.UpsertAsync(updated, TestContext.Current.CancellationToken);

        FederatedIdentity? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);
        found.ShouldNotBeNull();
        found.Username.ShouldBe("jdoe-updated");
        found.Email.ShouldBe("updated@test.com");
    }

    [Fact]
    public async Task FindByExternalIdsAsync_ReturnsBatchResults()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-2", username: "jane"), TestContext.Current.CancellationToken);

        IReadOnlyList<FederatedIdentity> results = await store.FindByExternalIdsAsync(
            ["user-1", "user-2", "user-3"], null, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FindFirstByExternalIdAsync_ReturnsEntryRegardlessOfTenant()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);
        var tenantId = Guid.NewGuid();

        await store.UpsertAsync(CreateEntry("user-1", tenantId: tenantId), TestContext.Current.CancellationToken);

        FederatedIdentity? result = await store.FindFirstByExternalIdAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task MultiTenantIsolation_SameUserDifferentTenants()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await store.UpsertAsync(CreateEntry("user-1", tenantId: tenant1, username: "tenant1-jdoe"),
            TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-1", tenantId: tenant2, username: "tenant2-jdoe"),
            TestContext.Current.CancellationToken);

        FederatedIdentity? fromTenant1 = await store.FindByExternalIdAsync(
            "user-1", tenant1, TestContext.Current.CancellationToken);
        FederatedIdentity? fromTenant2 = await store.FindByExternalIdAsync(
            "user-1", tenant2, TestContext.Current.CancellationToken);

        fromTenant1.ShouldNotBeNull();
        fromTenant1.Username.ShouldBe("tenant1-jdoe");
        fromTenant2.ShouldNotBeNull();
        fromTenant2.Username.ShouldBe("tenant2-jdoe");
    }

    [Fact]
    public async Task UpsertManyAsync_InsertsAndUpdatesInBatch()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        // Pre-insert one entry
        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);

        // Batch with update + new insert
        List<FederatedIdentity> entries =
        [
            CreateEntry("user-1", username: "updated"),
            CreateEntry("user-2", username: "new-user")
        ];

        await store.UpsertManyAsync(entries, TestContext.Current.CancellationToken);

        int count = await store.GetCountAsync(null, TestContext.Current.CancellationToken);
        count.ShouldBe(2);

        FederatedIdentity? user1 = await store.FindByExternalIdAsync("user-1", null, TestContext.Current.CancellationToken);
        user1!.Username.ShouldBe("updated");
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-2"), TestContext.Current.CancellationToken);

        int count = await store.GetCountAsync(null, TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetStaleCountAsync_CountsStaleEntries()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        FederatedIdentity fresh = CreateEntry("user-fresh");
        fresh.LastSyncedAt = DateTimeOffset.UtcNow;
        await store.UpsertAsync(fresh, TestContext.Current.CancellationToken);

        FederatedIdentity stale = CreateEntry("user-stale");
        stale.LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-2);
        await store.UpsertAsync(stale, TestContext.Current.CancellationToken);

        DateTimeOffset threshold = DateTimeOffset.UtcNow.AddDays(-1);
        int staleCount = await store.GetStaleCountAsync(null, threshold, TestContext.Current.CancellationToken);

        staleCount.ShouldBe(1);
    }

    [Fact]
    public async Task GetSyncRangeAsync_ReturnsOldestAndNewest()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        FederatedIdentity old = CreateEntry("user-old");
        old.LastSyncedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await store.UpsertAsync(old, TestContext.Current.CancellationToken);

        FederatedIdentity recent = CreateEntry("user-recent");
        recent.LastSyncedAt = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await store.UpsertAsync(recent, TestContext.Current.CancellationToken);

        (DateTimeOffset? oldest, DateTimeOffset? newest) = await store.GetSyncRangeAsync(
            null, TestContext.Current.CancellationToken);

        oldest.ShouldBe(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero));
        newest.ShouldBe(new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GetSyncRangeAsync_ReturnsNulls_WhenEmpty()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        (DateTimeOffset? oldest, DateTimeOffset? newest) = await store.GetSyncRangeAsync(
            null, TestContext.Current.CancellationToken);

        oldest.ShouldBeNull();
        newest.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByExternalIdAsync_RemovesEntry()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.DeleteByExternalIdAsync("user-1", null, TestContext.Current.CancellationToken);

        FederatedIdentity? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);
        found.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAllByTenantAsync_RemovesAllTenantEntries()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);
        var tenantId = Guid.NewGuid();

        await store.UpsertAsync(CreateEntry("user-1", tenantId: tenantId), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-2", tenantId: tenantId), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-3"), TestContext.Current.CancellationToken); // no tenant

        await store.DeleteAllByTenantAsync(tenantId, TestContext.Current.CancellationToken);

        int tenantCount = await store.GetCountAsync(tenantId, TestContext.Current.CancellationToken);
        int globalCount = await store.GetCountAsync(null, TestContext.Current.CancellationToken);

        tenantCount.ShouldBe(0);
        globalCount.ShouldBe(1);
    }

    [Fact]
    public async Task PseudonymizeAsync_ReplacesPersonalData()
    {
        await using TestDbContext context = CreateContext();
        EfCoreUserCacheStore<TestDbContext> store = CreateStore(context);

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.PseudonymizeAsync("user-1", null, TestContext.Current.CancellationToken);

        FederatedIdentity? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found.Username.ShouldBe("anonymized");
        found.Email.ShouldBe("anonymized@anonymized.local");
        found.FirstName.ShouldBe("Anonymized");
        found.LastName.ShouldBe("User");
        found.Enabled.ShouldBeFalse();
    }
}
