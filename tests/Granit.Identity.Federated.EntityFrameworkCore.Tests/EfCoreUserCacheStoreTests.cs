using Granit.Identity.Federated.EntityFrameworkCore.Entities;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

public sealed class EfCoreUserCacheStoreTests
{
    private static TestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserCacheEntry CreateEntry(
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
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        UserCacheEntry? result = await store.FindByExternalIdAsync(
            "nonexistent", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewEntry()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);
        UserCacheEntry entry = CreateEntry();

        await store.UpsertAsync(entry, TestContext.Current.CancellationToken);

        UserCacheEntry? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);
        found.ShouldNotBeNull();
        found.Username.ShouldBe("jdoe");
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingEntry()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);
        UserCacheEntry entry = CreateEntry();
        await store.UpsertAsync(entry, TestContext.Current.CancellationToken);

        UserCacheEntry updated = CreateEntry(username: "jdoe-updated", email: "updated@test.com");
        await store.UpsertAsync(updated, TestContext.Current.CancellationToken);

        UserCacheEntry? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);
        found.ShouldNotBeNull();
        found.Username.ShouldBe("jdoe-updated");
        found.Email.ShouldBe("updated@test.com");
    }

    [Fact]
    public async Task FindByExternalIdsAsync_ReturnsBatchResults()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-2", username: "jane"), TestContext.Current.CancellationToken);

        IReadOnlyList<UserCacheEntry> results = await store.FindByExternalIdsAsync(
            ["user-1", "user-2", "user-3"], null, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FindFirstByExternalIdAsync_ReturnsEntryRegardlessOfTenant()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);
        var tenantId = Guid.NewGuid();

        await store.UpsertAsync(CreateEntry("user-1", tenantId: tenantId), TestContext.Current.CancellationToken);

        UserCacheEntry? result = await store.FindFirstByExternalIdAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task MultiTenantIsolation_SameUserDifferentTenants()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await store.UpsertAsync(CreateEntry("user-1", tenantId: tenant1, username: "tenant1-jdoe"),
            TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-1", tenantId: tenant2, username: "tenant2-jdoe"),
            TestContext.Current.CancellationToken);

        UserCacheEntry? fromTenant1 = await store.FindByExternalIdAsync(
            "user-1", tenant1, TestContext.Current.CancellationToken);
        UserCacheEntry? fromTenant2 = await store.FindByExternalIdAsync(
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
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        // Pre-insert one entry
        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);

        // Batch with update + new insert
        List<UserCacheEntry> entries =
        [
            CreateEntry("user-1", username: "updated"),
            CreateEntry("user-2", username: "new-user")
        ];

        await store.UpsertManyAsync(entries, TestContext.Current.CancellationToken);

        int count = await store.GetCountAsync(null, TestContext.Current.CancellationToken);
        count.ShouldBe(2);

        UserCacheEntry? user1 = await store.FindByExternalIdAsync("user-1", null, TestContext.Current.CancellationToken);
        user1!.Username.ShouldBe("updated");
    }

    [Fact]
    public async Task GetCountAsync_ReturnsCorrectCount()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-2"), TestContext.Current.CancellationToken);

        int count = await store.GetCountAsync(null, TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetStaleCountAsync_CountsStaleEntries()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        UserCacheEntry fresh = CreateEntry("user-fresh");
        fresh.LastSyncedAt = DateTimeOffset.UtcNow;
        await store.UpsertAsync(fresh, TestContext.Current.CancellationToken);

        UserCacheEntry stale = CreateEntry("user-stale");
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
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        UserCacheEntry old = CreateEntry("user-old");
        old.LastSyncedAt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await store.UpsertAsync(old, TestContext.Current.CancellationToken);

        UserCacheEntry recent = CreateEntry("user-recent");
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
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        (DateTimeOffset? oldest, DateTimeOffset? newest) = await store.GetSyncRangeAsync(
            null, TestContext.Current.CancellationToken);

        oldest.ShouldBeNull();
        newest.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByExternalIdAsync_RemovesEntry()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.DeleteByExternalIdAsync("user-1", null, TestContext.Current.CancellationToken);

        UserCacheEntry? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);
        found.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAllByTenantAsync_RemovesAllTenantEntries()
    {
        await using TestDbContext context = CreateContext();
        var store = new EfCoreUserCacheStore<TestDbContext>(context);
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
        var store = new EfCoreUserCacheStore<TestDbContext>(context);

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.PseudonymizeAsync("user-1", null, TestContext.Current.CancellationToken);

        UserCacheEntry? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);

        found.ShouldNotBeNull();
        found.Username.ShouldBe("anonymized");
        found.Email.ShouldBe("anonymized@anonymized.local");
        found.FirstName.ShouldBe("Anonymized");
        found.LastName.ShouldBe("User");
        found.Enabled.ShouldBeFalse();
    }
}
