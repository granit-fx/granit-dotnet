using System.Text.Json;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Encryption;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

/// <summary>
/// Pins the V3 cross-tenant routing contract for Notifications under
/// <see cref="DualScopeStorageMode.Segregated"/>: writes route to the correct DB based on
/// the entity's <c>TenantId</c>, and reads by id fan out across both contexts.
/// </summary>
public sealed class SegregatedCrossTenantAggregationTests : IDisposable
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly DataFilter _filter = new();
    private readonly IDisposable _filterDisable;
    private readonly IStringEncryptionService _encryption = new PassthroughEncryption();

    public SegregatedCrossTenantAggregationTests()
        => _filterDisable = _filter.Disable<IMultiTenant>();

    public void Dispose() => _filterDisable.Dispose();

    [Fact]
    public async Task UserNotificationStore_Insert_TenantScoped_RoutesToTenantContext()
    {
        DbContextOptions<NotificationsHostDbContext> hostOpts = InMemoryHostOptions("notif-host-route");
        DbContextOptions<NotificationsTenantDbContext> tenantOpts = InMemoryTenantOptions("notif-tenant-route");

        IDbContextFactory<NotificationsHostDbContext> hostFactory = StubHostFactory(hostOpts);
        IDbContextFactory<NotificationsTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts);
        NotificationsContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        EfCoreUserNotificationStore store = new(resolver);

        UserNotification tenantNotification = NewNotification(tenantId: _tenantA);
        await store.InsertAsync(tenantNotification, TestContext.Current.CancellationToken);

        // Tenant DB should hold it; host DB should not.
        await using NotificationsHostDbContext hostCtx = new(hostOpts, _encryption, GranitDesignTime.CurrentTenant, _filter);
        (await hostCtx.UserNotifications.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(0);

        await using NotificationsTenantDbContext tenantCtx = new(tenantOpts, _encryption, GranitDesignTime.CurrentTenant, _filter);
        UserNotification persisted = (await tenantCtx.UserNotifications
            .IgnoreQueryFilters()
            .ToListAsync(TestContext.Current.CancellationToken))
            .ShouldHaveSingleItem();
        persisted.Id.ShouldBe(tenantNotification.Id);
        persisted.TenantId.ShouldBe(_tenantA);
    }

    [Fact]
    public async Task UserNotificationStore_Insert_HostScoped_RoutesToHostContext()
    {
        DbContextOptions<NotificationsHostDbContext> hostOpts = InMemoryHostOptions("notif-host-route-h");
        DbContextOptions<NotificationsTenantDbContext> tenantOpts = InMemoryTenantOptions("notif-tenant-route-h");

        IDbContextFactory<NotificationsHostDbContext> hostFactory = StubHostFactory(hostOpts);
        IDbContextFactory<NotificationsTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts);
        NotificationsContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        EfCoreUserNotificationStore store = new(resolver);

        UserNotification hostNotification = NewNotification(tenantId: null);
        await store.InsertAsync(hostNotification, TestContext.Current.CancellationToken);

        await using NotificationsHostDbContext hostCtx = new(hostOpts, _encryption, GranitDesignTime.CurrentTenant, _filter);
        (await hostCtx.UserNotifications.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(1);

        await using NotificationsTenantDbContext tenantCtx = new(tenantOpts, _encryption, GranitDesignTime.CurrentTenant, _filter);
        (await tenantCtx.UserNotifications.IgnoreQueryFilters().CountAsync(TestContext.Current.CancellationToken))
            .ShouldBe(0);
    }

    [Fact]
    public async Task UserNotificationStore_GetById_FansOutAcrossBothContexts()
    {
        DbContextOptions<NotificationsHostDbContext> hostOpts = InMemoryHostOptions("notif-host-getid");
        DbContextOptions<NotificationsTenantDbContext> tenantOpts = InMemoryTenantOptions("notif-tenant-getid");

        UserNotification hostNotification = NewNotification(tenantId: null);
        UserNotification tenantNotification = NewNotification(tenantId: _tenantA);

        await SeedHostAsync(hostOpts, hostNotification);
        await SeedTenantAsync(tenantOpts, tenantNotification);

        IDbContextFactory<NotificationsHostDbContext> hostFactory = StubHostFactory(hostOpts);
        IDbContextFactory<NotificationsTenantDbContext> tenantFactory = StubTenantFactory(tenantOpts);
        NotificationsContextResolver resolver = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        EfCoreUserNotificationStore store = new(resolver);

        UserNotification? foundHost = await store.GetAsync(hostNotification.Id, TestContext.Current.CancellationToken);
        UserNotification? foundTenant = await store.GetAsync(tenantNotification.Id, TestContext.Current.CancellationToken);

        foundHost.ShouldNotBeNull();
        foundHost!.Id.ShouldBe(hostNotification.Id);
        foundTenant.ShouldNotBeNull();
        foundTenant!.Id.ShouldBe(tenantNotification.Id);
    }

    // ---- helpers -----------------------------------------------------------

    private static DbContextOptions<NotificationsHostDbContext> InMemoryHostOptions(string name)
        => new DbContextOptionsBuilder<NotificationsHostDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private static DbContextOptions<NotificationsTenantDbContext> InMemoryTenantOptions(string name)
        => new DbContextOptionsBuilder<NotificationsTenantDbContext>()
            .UseInMemoryDatabase($"{name}-{Guid.NewGuid()}")
            .Options;

    private async Task SeedHostAsync(
        DbContextOptions<NotificationsHostDbContext> opts,
        params UserNotification[] entries)
    {
        await using NotificationsHostDbContext db = new(opts, _encryption, GranitDesignTime.CurrentTenant, _filter);
        db.UserNotifications.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task SeedTenantAsync(
        DbContextOptions<NotificationsTenantDbContext> opts,
        params UserNotification[] entries)
    {
        await using NotificationsTenantDbContext db = new(opts, _encryption, GranitDesignTime.CurrentTenant, _filter);
        db.UserNotifications.AddRange(entries);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private IDbContextFactory<NotificationsHostDbContext> StubHostFactory(
        DbContextOptions<NotificationsHostDbContext> opts)
    {
        IDbContextFactory<NotificationsHostDbContext> factory = Substitute.For<IDbContextFactory<NotificationsHostDbContext>>();
        factory.CreateDbContext().Returns(_ => new NotificationsHostDbContext(opts, _encryption, GranitDesignTime.CurrentTenant, _filter));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new NotificationsHostDbContext(opts, _encryption, GranitDesignTime.CurrentTenant, _filter)));
        return factory;
    }

    private IDbContextFactory<NotificationsTenantDbContext> StubTenantFactory(
        DbContextOptions<NotificationsTenantDbContext> opts)
    {
        IDbContextFactory<NotificationsTenantDbContext> factory = Substitute.For<IDbContextFactory<NotificationsTenantDbContext>>();
        factory.CreateDbContext().Returns(_ => new NotificationsTenantDbContext(opts, _encryption, GranitDesignTime.CurrentTenant, _filter));
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new NotificationsTenantDbContext(opts, _encryption, GranitDesignTime.CurrentTenant, _filter)));
        return factory;
    }

    private static UserNotification NewNotification(Guid? tenantId) =>
        UserNotification.Create(
            id: Guid.NewGuid(),
            notificationId: Guid.NewGuid(),
            notificationTypeName: "test.notif",
            severity: NotificationSeverity.Info,
            recipientUserId: "user-1",
            data: JsonSerializer.SerializeToElement(new { key = "value" }),
            createdAt: DateTimeOffset.UtcNow,
            tenantId: tenantId);

    private sealed class PassthroughEncryption : IStringEncryptionService
    {
        public string Encrypt(string plainText) => plainText;
        public string? Decrypt(string cipherText) => cipherText;
    }
}
