using Granit.DataFiltering;
using Granit.Domain;
using Granit.Encryption;
using Granit.Identity.Domain;
using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Identity.EntityFrameworkCore.Options;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.EntityFrameworkCore.Tests;

/// <summary>
/// Pins the cross-tenant aggregation contract for V2 of Epic #2382: under
/// <see cref="DualScopeStorageMode.Segregated"/> + host-admin scope,
/// <see cref="EfUserDirectoryQueryableSource"/> iterates every tenant via
/// <see cref="ITenantsAccessor"/> + <see cref="ICurrentTenant.Change"/>.
/// </summary>
public sealed class SegregatedCrossTenantAggregationTests
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly DataFilter _filter = new();
    private readonly IStringEncryptionService _encryption = new PassthroughEncryption();

    [Fact]
    public async Task UserDirectoryQueryable_HostAdmin_MaterialisesHostPlusEveryTenant()
    {
        DbContextOptions<IdentityHostDbContext> hostOpts = InMemoryHostOptions("users-host");
        DbContextOptions<IdentityTenantDbContext> tenantOpts = InMemoryTenantOptions("users-tenant");

        await SeedHostAsync(hostOpts, NewUser("alice@host", "Alice", tenantId: null));
        await SeedTenantAsync(tenantOpts,
            NewUser("bob@a", "Bob", _tenantA),
            NewUser("carol@b", "Carol", _tenantB));

        IDbContextFactory<IdentityHostDbContext> hostFactory = StubHostFactory(hostOpts);
        ICurrentTenant currentTenant = NewSwitchableTenant(initiallyAvailable: false);
        IDbContextFactory<IdentityTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts, currentTenant);
        ITenantsAccessor tenantsAccessor = StubTenantsAccessor(_tenantA, _tenantB);

        using EfUserDirectoryQueryableSource sut = new(
            new IdentityEntityFrameworkCoreOptions { StorageMode = DualScopeStorageMode.Segregated },
            currentTenant,
            tenantsAccessor,
            hostFactory,
            tenantFactory);

        User[] result = [.. sut.GetQueryable()];

        // Host row + tenant A row (active when iterating) + tenant B row (active when iterating).
        result.Length.ShouldBe(3);
        result.Count(u => u.TenantId is null).ShouldBe(1);
        result.Count(u => u.TenantId == _tenantA).ShouldBe(1);
        result.Count(u => u.TenantId == _tenantB).ShouldBe(1);
    }

    [Fact]
    public async Task UserDirectoryQueryable_HostAdmin_EmptyTenantsAccessor_ReturnsHostOnly()
    {
        DbContextOptions<IdentityHostDbContext> hostOpts = InMemoryHostOptions("users-host-noaccessor");
        DbContextOptions<IdentityTenantDbContext> tenantOpts = InMemoryTenantOptions("users-tenant-noaccessor");

        await SeedHostAsync(hostOpts, NewUser("admin@host", "Admin", tenantId: null));
        await SeedTenantAsync(tenantOpts, NewUser("ghost@a", "Ghost", _tenantA));

        IDbContextFactory<IdentityHostDbContext> hostFactory = StubHostFactory(hostOpts);
        ICurrentTenant currentTenant = NewSwitchableTenant(initiallyAvailable: false);
        IDbContextFactory<IdentityTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts, currentTenant);

        using EfUserDirectoryQueryableSource sut = new(
            new IdentityEntityFrameworkCoreOptions { StorageMode = DualScopeStorageMode.Segregated },
            currentTenant,
            tenantsAccessor: StubTenantsAccessor() /* empty — NullTenantsAccessor style */,
            hostFactory,
            tenantFactory);

        User[] result = [.. sut.GetQueryable()];

        result.Length.ShouldBe(1);
        result[0].TenantId.ShouldBeNull();
    }

    // ---- helpers -----------------------------------------------------------

    private static DbContextOptions<IdentityHostDbContext> InMemoryHostOptions(string name)
        => new DbContextOptionsBuilder<IdentityHostDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private static DbContextOptions<IdentityTenantDbContext> InMemoryTenantOptions(string name)
        => new DbContextOptionsBuilder<IdentityTenantDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private async Task SeedHostAsync(
        DbContextOptions<IdentityHostDbContext> opts,
        params User[] users)
    {
        using IDisposable _ = _filter.Disable<IMultiTenant>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        await using IdentityHostDbContext db = new(opts, _encryption, tenant, _filter);
        db.Users.AddRange(users);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedTenantAsync(
        DbContextOptions<IdentityTenantDbContext> opts,
        params User[] users)
    {
        using IDisposable _ = _filter.Disable<IMultiTenant>();
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(false);
        await using IdentityTenantDbContext db = new(opts, _encryption, tenant, _filter);
        db.Users.AddRange(users);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private IDbContextFactory<IdentityHostDbContext> StubHostFactory(
        DbContextOptions<IdentityHostDbContext> opts)
    {
        IDbContextFactory<IdentityHostDbContext> factory = Substitute.For<IDbContextFactory<IdentityHostDbContext>>();
        factory.CreateDbContext().Returns(_ => new IdentityHostDbContext(opts, _encryption, GranitDesignTime.CurrentTenant));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new IdentityHostDbContext(opts, _encryption, GranitDesignTime.CurrentTenant)));
        return factory;
    }

    private IDbContextFactory<IdentityTenantDbContext> StubTenantFactory(
        DbContextOptions<IdentityTenantDbContext> opts,
        ICurrentTenant currentTenant)
    {
        IDbContextFactory<IdentityTenantDbContext> factory = Substitute.For<IDbContextFactory<IdentityTenantDbContext>>();
        factory.CreateDbContext().Returns(_ => new IdentityTenantDbContext(opts, _encryption, currentTenant, _filter));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new IdentityTenantDbContext(opts, _encryption, currentTenant, _filter)));
        return factory;
    }

    private static ITenantsAccessor StubTenantsAccessor(params Guid[] tenantIds)
    {
        ITenantsAccessor accessor = Substitute.For<ITenantsAccessor>();
        (Guid Id, string Name)[] tenants = [.. tenantIds.Select(id => (id, $"tenant-{id}"))];
        accessor.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid, string)>>(tenants));
        return accessor;
    }

    private static ICurrentTenant NewSwitchableTenant(bool initiallyAvailable)
    {
        Guid? currentId = null;
        bool available = initiallyAvailable;

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(_ => available);
        tenant.Id.Returns(_ => currentId);
        tenant.Change(Arg.Any<Guid?>(), Arg.Any<string?>())
            .Returns(call =>
            {
                Guid? previousId = currentId;
                bool previousAvail = available;
                currentId = call.ArgAt<Guid?>(0);
                available = currentId is not null;
                return new ChangeScope(() => { currentId = previousId; available = previousAvail; });
            });
        return tenant;
    }

    private sealed class ChangeScope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private static User NewUser(string email, string displayName, Guid? tenantId)
        => User.Create(Guid.NewGuid(), email, displayName, tenantId: tenantId);

    private sealed class PassthroughEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string? Decrypt(string cipherText) => cipherText;
    }
}
