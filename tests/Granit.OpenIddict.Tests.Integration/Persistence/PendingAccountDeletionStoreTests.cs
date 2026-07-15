using Granit.Identity.Local.Domain;
using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Services;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // EfPendingAccountDeletionStore / OpenIddictDbContext are internal — accessible via InternalsVisibleTo

namespace Granit.OpenIddict.Tests.Integration.Persistence;

/// <summary>
/// Validates that <see cref="EfPendingAccountDeletionStore"/> finds soft-deleted, undispatched users
/// across every tenant and stops returning them once marked. Needs real PostgreSQL: the query bypasses
/// both the multi-tenant and the soft-delete query filters, and marking uses a bulk
/// <c>ExecuteUpdate</c> — neither is faithfully exercised by mocks.
/// </summary>
public sealed class PendingAccountDeletionStoreTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset DeletedAt = DateTimeOffset.UnixEpoch;

    private readonly PostgresFixture _postgres = new();
    private Factory? _factory;

    private EfPendingAccountDeletionStore Store => new(_factory!);

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        DbContextOptions<OpenIddictDbContext> options =
            new DbContextOptionsBuilder<OpenIddictDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options;
        _factory = new Factory(options);

        await using OpenIddictDbContext db = _factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task GetPending_ReturnsSoftDeletedUndispatched_AcrossTenants()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid globalPending = await SeedAsync(tenantId: null, deleted: true, dispatched: false, ct);
        Guid tenantPending = await SeedAsync(tenantId: TenantA, deleted: true, dispatched: false, ct);
        await SeedAsync(tenantId: null, deleted: false, dispatched: false, ct);       // not deleted
        await SeedAsync(tenantId: TenantA, deleted: true, dispatched: true, ct);      // already dispatched

        IReadOnlyList<PendingAccountDeletion> pending = await Store.GetPendingAsync(100, ct);

        pending.Select(p => p.UserId).ShouldBe([globalPending, tenantPending], ignoreOrder: true);
        pending.Single(p => p.UserId == tenantPending).TenantId.ShouldBe(TenantA);
        pending.Single(p => p.UserId == globalPending).TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task MarkDispatched_ExcludesFromNextSweep()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid userId = await SeedAsync(tenantId: TenantA, deleted: true, dispatched: false, ct);

        (await Store.GetPendingAsync(100, ct)).ShouldContain(p => p.UserId == userId);

        await Store.MarkDispatchedAsync(userId, DateTimeOffset.UnixEpoch.AddDays(1), ct);

        (await Store.GetPendingAsync(100, ct)).ShouldNotContain(p => p.UserId == userId);
    }

    private async Task<Guid> SeedAsync(Guid? tenantId, bool deleted, bool dispatched, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using OpenIddictDbContext db = _factory!.CreateDbContext();
        db.Set<LocalIdentity>().Add(new LocalIdentity
        {
            Id = id,
            UserName = id.ToString("N"),
            NormalizedUserName = id.ToString("N").ToUpperInvariant(),
            Email = $"{id:N}@example.com",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            IsDeleted = deleted,
            DeletedAt = deleted ? DeletedAt : null,
            DeletionEventDispatchedAt = dispatched ? DeletedAt : null,
        });
        await db.SaveChangesAsync(ct);
        return id;
    }

    private sealed class Factory(DbContextOptions<OpenIddictDbContext> options)
        : IDbContextFactory<OpenIddictDbContext>
    {
        public OpenIddictDbContext CreateDbContext() => new(options, NullTenantContext.Instance);
    }
}

#pragma warning restore EF1001
