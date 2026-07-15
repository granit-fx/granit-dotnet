using Granit.Identity.Federated.Domain;
using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Identity.Federated.Internal;
using Granit.Testing.Fakes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntityFrameworkCore.Tests;

/// <summary>
/// SQLite-backed store tests with the <c>IMultiTenant</c> query filter ACTIVE, mirroring
/// production. The EF Core In-Memory provider silently ignores query filters, which is
/// exactly how the tenant-scoped GDPR-erasure no-op stayed invisible to CI — tests that
/// touch tenant-scoped rows must establish an ambient tenant via
/// <see cref="FakeCurrentTenant.Change"/>, the same contract production callers follow.
/// </summary>
public sealed class EfCoreUserCacheStoreTests : IDisposable
{
    private static readonly IUserLookupHasher Hasher = CreateHasher();
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakeCurrentTenant _tenant = new();

    public EfCoreUserCacheStoreTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

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

    private EfCoreUserCacheStore CreateStore()
    {
        DbContextOptions<IdentityFederatedDbContext> options = new DbContextOptionsBuilder<IdentityFederatedDbContext>()
            .UseSqlite(_connection)
            .Options;
        TestIdentityFederatedDbContextFactory factory = new(options, dataFilter: null, _tenant);

        using (IdentityFederatedDbContext db = factory.CreateDbContext())
        {
            db.Database.EnsureCreated();
        }

        return new EfCoreUserCacheStore(factory, Hasher);
    }

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

    private async Task UpsertScopedAsync(EfCoreUserCacheStore store, FederatedIdentity entry)
    {
        using IDisposable _ = _tenant.Change(entry.TenantId);
        await store.UpsertAsync(entry, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FindByExternalIdAsync_ReturnsNull_WhenNotFound()
    {
        EfCoreUserCacheStore store = CreateStore();

        FederatedIdentity? result = await store.FindByExternalIdAsync(
            "nonexistent", null, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task UpsertAsync_InsertsNewEntry()
    {
        EfCoreUserCacheStore store = CreateStore();
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
        EfCoreUserCacheStore store = CreateStore();
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
        EfCoreUserCacheStore store = CreateStore();

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-2", username: "jane"), TestContext.Current.CancellationToken);

        IReadOnlyList<FederatedIdentity> results = await store.FindByExternalIdsAsync(
            ["user-1", "user-2", "user-3"], null, TestContext.Current.CancellationToken);

        results.Count.ShouldBe(2);
    }

    [Fact]
    public async Task FindFirstByExternalIdAsync_ReturnsEntryRegardlessOfTenant()
    {
        EfCoreUserCacheStore store = CreateStore();
        var tenantId = Guid.NewGuid();

        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenantId));

        // Host context (no ambient tenant): the documented contract is "regardless of
        // tenant" — the store must bypass the multi-tenant filter or the cache-aside
        // caller re-inserts a duplicate host-scope row for an already-mirrored user.
        FederatedIdentity? result = await store.FindFirstByExternalIdAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task MultiTenantIsolation_SameUserDifferentTenants()
    {
        EfCoreUserCacheStore store = CreateStore();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenant1, username: "tenant1-jdoe"));
        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenant2, username: "tenant2-jdoe"));

        using (_tenant.Change(tenant1))
        {
            FederatedIdentity? fromTenant1 = await store.FindByExternalIdAsync(
                "user-1", tenant1, TestContext.Current.CancellationToken);
            fromTenant1.ShouldNotBeNull();
            fromTenant1.Username.ShouldBe("tenant1-jdoe");
        }

        using (_tenant.Change(tenant2))
        {
            FederatedIdentity? fromTenant2 = await store.FindByExternalIdAsync(
                "user-1", tenant2, TestContext.Current.CancellationToken);
            fromTenant2.ShouldNotBeNull();
            fromTenant2.Username.ShouldBe("tenant2-jdoe");
        }
    }

    [Fact]
    public async Task MultiTenantFilter_HidesOtherTenantsRows()
    {
        EfCoreUserCacheStore store = CreateStore();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenant1));

        // Reading tenant1's row under tenant2's ambient scope must fail closed — this is
        // the row-level isolation the In-Memory provider never exercised.
        using (_tenant.Change(tenant2))
        {
            FederatedIdentity? crossTenant = await store.FindByExternalIdAsync(
                "user-1", tenant1, TestContext.Current.CancellationToken);
            crossTenant.ShouldBeNull();
        }
    }

    [Fact]
    public async Task UpsertManyAsync_InsertsAndUpdatesInBatch()
    {
        EfCoreUserCacheStore store = CreateStore();

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
        EfCoreUserCacheStore store = CreateStore();

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.UpsertAsync(CreateEntry("user-2"), TestContext.Current.CancellationToken);

        int count = await store.GetCountAsync(null, TestContext.Current.CancellationToken);
        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetSyncRangeAsync_ReturnsNulls_WhenEmpty()
    {
        EfCoreUserCacheStore store = CreateStore();

        (DateTimeOffset? oldest, DateTimeOffset? newest) = await store.GetSyncRangeAsync(
            null, TestContext.Current.CancellationToken);

        oldest.ShouldBeNull();
        newest.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByExternalIdAsync_RemovesEntry()
    {
        EfCoreUserCacheStore store = CreateStore();

        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await store.DeleteByExternalIdAsync("user-1", null, TestContext.Current.CancellationToken);

        FederatedIdentity? found = await store.FindByExternalIdAsync(
            "user-1", null, TestContext.Current.CancellationToken);
        found.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByExternalIdAsync_NullTenant_SweepsAllTenantPartitions()
    {
        // GDPR Art. 17 regression (audit BREAKING #2): a null tenant scope is documented on
        // IdentityUserDeletedEto as "deletes across all tenants" but used to translate to
        // "TenantId IS NULL" — host rows only — leaving every tenant mirror in place while
        // the erasure acked. Red on the old implementation, green with the explicit sweep.
        EfCoreUserCacheStore store = CreateStore();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenant1));
        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenant2));
        await store.UpsertAsync(CreateEntry("user-1"), TestContext.Current.CancellationToken);
        await UpsertScopedAsync(store, CreateEntry("user-2", tenantId: tenant1, username: "survivor"));

        await store.DeleteByExternalIdAsync("user-1", null, TestContext.Current.CancellationToken);

        using (_tenant.Change(tenant1))
        {
            (await store.FindByExternalIdAsync("user-1", tenant1, TestContext.Current.CancellationToken))
                .ShouldBeNull();
            (await store.FindByExternalIdAsync("user-2", tenant1, TestContext.Current.CancellationToken))
                .ShouldNotBeNull();
        }

        using (_tenant.Change(tenant2))
        {
            (await store.FindByExternalIdAsync("user-1", tenant2, TestContext.Current.CancellationToken))
                .ShouldBeNull();
        }

        (await store.FindByExternalIdAsync("user-1", null, TestContext.Current.CancellationToken))
            .ShouldBeNull();
    }

    [Fact]
    public async Task DeleteByExternalIdAsync_TenantScoped_DeletesOnlyThatTenant_AndReturnsCount()
    {
        EfCoreUserCacheStore store = CreateStore();
        var tenant1 = Guid.NewGuid();
        var tenant2 = Guid.NewGuid();

        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenant1));
        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenant2));

        int deleted;
        using (_tenant.Change(tenant1))
        {
            deleted = await store.DeleteByExternalIdAsync(
                "user-1", tenant1, TestContext.Current.CancellationToken);
        }

        deleted.ShouldBe(1);
        using (_tenant.Change(tenant2))
        {
            (await store.FindByExternalIdAsync("user-1", tenant2, TestContext.Current.CancellationToken))
                .ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task DeleteByExternalIdAsync_TenantScoped_WithoutAmbientScope_FailsClosed()
    {
        // Pins the residual sharp edge until the tenant-explicit store seam lands: a
        // tenant-scoped delete issued without a matching ambient tenant must not touch the
        // row (the filter hides it — fail closed, 0 affected). Callers are responsible for
        // establishing scope via ICurrentTenant.Change, as the Wolverine handlers now do.
        EfCoreUserCacheStore store = CreateStore();
        var tenant1 = Guid.NewGuid();

        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenant1));

        int deleted = await store.DeleteByExternalIdAsync(
            "user-1", tenant1, TestContext.Current.CancellationToken);

        deleted.ShouldBe(0);
        using (_tenant.Change(tenant1))
        {
            (await store.FindByExternalIdAsync("user-1", tenant1, TestContext.Current.CancellationToken))
                .ShouldNotBeNull();
        }
    }

    [Fact]
    public async Task DeleteAllByTenantAsync_RemovesAllTenantEntries()
    {
        EfCoreUserCacheStore store = CreateStore();
        var tenantId = Guid.NewGuid();

        await UpsertScopedAsync(store, CreateEntry("user-1", tenantId: tenantId));
        await UpsertScopedAsync(store, CreateEntry("user-2", tenantId: tenantId));
        await store.UpsertAsync(CreateEntry("user-3"), TestContext.Current.CancellationToken); // no tenant

        using (_tenant.Change(tenantId))
        {
            await store.DeleteAllByTenantAsync(tenantId, TestContext.Current.CancellationToken);

            int tenantCount = await store.GetCountAsync(tenantId, TestContext.Current.CancellationToken);
            tenantCount.ShouldBe(0);
        }

        int globalCount = await store.GetCountAsync(null, TestContext.Current.CancellationToken);
        globalCount.ShouldBe(1);
    }

    [Fact]
    public async Task PseudonymizeAsync_ReplacesPersonalData()
    {
        EfCoreUserCacheStore store = CreateStore();

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
