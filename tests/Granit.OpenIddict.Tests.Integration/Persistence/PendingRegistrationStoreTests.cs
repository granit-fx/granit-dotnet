using Granit.Identity.Local.Domain;
using Granit.Identity.Local.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Services;
using Granit.OpenIddict.Tests.Integration.Fixtures;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

#pragma warning disable EF1001 // EfPendingRegistrationStore / IdentityLocalDbContext are internal — accessible via InternalsVisibleTo

namespace Granit.OpenIddict.Tests.Integration.Persistence;

/// <summary>
/// Validates that <see cref="EfPendingRegistrationStore"/> finds accounts with a pending registration
/// event across every tenant, excludes soft-deleted and already-cleared accounts, and stops returning
/// an account once marked. Needs real PostgreSQL: the query bypasses the multi-tenant filter while
/// keeping the soft-delete filter, and marking uses a bulk <c>ExecuteUpdate</c> — neither is
/// faithfully exercised by mocks.
/// </summary>
public sealed class PendingRegistrationStoreTests : IAsyncLifetime
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset PendingSince = DateTimeOffset.UnixEpoch;

    private readonly PostgresFixture _postgres = new();
    private Factory? _factory;

    private EfPendingRegistrationStore Store => new(_factory!);

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        DbContextOptions<IdentityLocalDbContext> options =
            new DbContextOptionsBuilder<IdentityLocalDbContext>()
                .UseNpgsql(_postgres.ConnectionString)
                .Options;
        _factory = new Factory(options);

        await using IdentityLocalDbContext db = _factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task GetPending_ReturnsPending_AcrossTenants_ExcludingDeletedAndCleared()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid globalPending = await SeedAsync(tenantId: null, pending: true, deleted: false, ct);
        Guid tenantPending = await SeedAsync(tenantId: TenantA, pending: true, deleted: false, ct);
        await SeedAsync(tenantId: null, pending: false, deleted: false, ct);   // already dispatched/other means
        await SeedAsync(tenantId: TenantA, pending: true, deleted: true, ct);  // pending but soft-deleted → moot

        IReadOnlyList<PendingRegistration> pending = await Store.GetPendingAsync(100, ct);

        pending.Select(p => p.UserId).ShouldBe([globalPending, tenantPending], ignoreOrder: true);
        pending.Single(p => p.UserId == tenantPending).TenantId.ShouldBe(TenantA);
        pending.Single(p => p.UserId == globalPending).TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task MarkDispatched_ExcludesFromNextSweep()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        Guid userId = await SeedAsync(tenantId: TenantA, pending: true, deleted: false, ct);

        (await Store.GetPendingAsync(100, ct)).ShouldContain(p => p.UserId == userId);

        await Store.MarkDispatchedAsync(userId, ct);

        (await Store.GetPendingAsync(100, ct)).ShouldNotContain(p => p.UserId == userId);
    }

    private async Task<Guid> SeedAsync(Guid? tenantId, bool pending, bool deleted, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await using IdentityLocalDbContext db = _factory!.CreateDbContext();
        db.Set<LocalIdentity>().Add(new LocalIdentity
        {
            Id = id,
            UserName = id.ToString("N"),
            NormalizedUserName = id.ToString("N").ToUpperInvariant(),
            Email = $"{id:N}@example.com",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            TenantId = tenantId,
            IsDeleted = deleted,
            DeletedAt = deleted ? PendingSince : null,
            RegistrationEventPendingSince = pending ? PendingSince : null,
        });
        await db.SaveChangesAsync(ct);
        return id;
    }

    private sealed class Factory(DbContextOptions<IdentityLocalDbContext> options)
        : IDbContextFactory<IdentityLocalDbContext>
    {
        public IdentityLocalDbContext CreateDbContext() => new(options, NullTenantContext.Instance);
    }
}

#pragma warning restore EF1001
