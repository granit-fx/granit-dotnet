using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

/// <summary>
/// Behavioural tests for <see cref="EfWebhookSubscriptionQueryableSource"/> — the
/// host-bypass piece of the dual-scope contract documented in #2021. These tests pin
/// the row-level filter semantics so a future refactor can't silently drop the bypass
/// (which would force host admin reads through the tenant filter and hide cross-tenant
/// rows).
/// </summary>
public sealed class EfWebhookSubscriptionQueryableSourceTests
{
    private readonly Guid _tenantA = Guid.NewGuid();
    private readonly Guid _tenantB = Guid.NewGuid();
    private readonly DbContextOptions<WebhooksHostDbContext> _options;
    private readonly DataFilter _filter = new();

    public EfWebhookSubscriptionQueryableSourceTests()
    {
        _options = new DbContextOptionsBuilder<WebhooksHostDbContext>()
            .UseInMemoryDatabase(databaseName: $"webhooks-qs-{Guid.NewGuid()}")
            .Options;
    }

    [Fact]
    public async Task GetQueryable_TenantContext_ReturnsTenantRows()
    {
        // Arrange — seed tenant A, tenant B, and a platform (null) subscription with the
        // MultiTenant filter temporarily disabled so we can write across tenants.
        await SeedAcrossTenantsAsync(
            CreateSubscription("doc.uploaded", _tenantA),
            CreateSubscription("doc.uploaded", _tenantB),
            CreateSubscription("doc.uploaded", tenantId: null));

        // Tenant A is active — both at the DbContext level (filter parameter) and at the
        // QueryableSource level (no bypass).
        ICurrentTenant tenantA = TenantStub(_tenantA, isAvailable: true);
        WebhooksEntityFrameworkCoreOptions opts = new() { StorageMode = DualScopeStorageMode.Shared };
        var sut = new EfWebhookSubscriptionQueryableSource(
            opts,
            tenantA,
            EmptyAccessor(),
            new TenantScopedDbContextFactory(_options, tenantA, _filter));

        // Act
        WebhookSubscription[] result = [.. sut.GetQueryable()];

        // Assert — tenant B's row is gone; tenant A's row remains.
        result.ShouldHaveSingleItem();
        result[0].TenantId.ShouldBe(_tenantA);
    }

    [Fact]
    public async Task GetQueryable_HostContext_ReturnsAllTenants()
    {
        // Arrange
        await SeedAcrossTenantsAsync(
            CreateSubscription("doc.uploaded", _tenantA),
            CreateSubscription("doc.uploaded", _tenantB),
            CreateSubscription("doc.uploaded", tenantId: null));

        // No tenant — host admin path. IgnoreQueryFilters([MultiTenant]) bypasses the
        // row-level filter and returns every subscription.
        ICurrentTenant host = TenantStub(tenantId: null, isAvailable: false);
        WebhooksEntityFrameworkCoreOptions opts = new() { StorageMode = DualScopeStorageMode.Shared };
        var sut = new EfWebhookSubscriptionQueryableSource(
            opts,
            host,
            EmptyAccessor(),
            new TenantScopedDbContextFactory(_options, host, _filter));

        // Act
        WebhookSubscription[] result = [.. sut.GetQueryable()];

        // Assert — all three rows visible to the host context.
        result.Length.ShouldBe(3);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task SeedAcrossTenantsAsync(params WebhookSubscription[] subscriptions)
    {
        using IDisposable _ = _filter.Disable<IMultiTenant>();
        await using WebhooksHostDbContext context = new(_options, TenantStub(tenantId: null, isAvailable: false), _filter);
        context.WebhookSubscriptions.AddRange(subscriptions);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static ICurrentTenant TenantStub(Guid? tenantId, bool isAvailable)
    {
        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.IsAvailable.Returns(isAvailable);
        tenant.Id.Returns(tenantId);
        return tenant;
    }

    private static WebhookSubscription CreateSubscription(string eventType, Guid? tenantId) =>
        WebhookSubscription.Create(
            Guid.NewGuid(),
            $"https://example.com/{Guid.NewGuid()}",
            eventType,
            signingKeyId: Guid.NewGuid(),
            protectedSecret: "protected-secret",
            createdAt: DateTimeOffset.UtcNow,
            tenantId: tenantId);


    private static ITenantsAccessor EmptyAccessor()
    {
        ITenantsAccessor e = Substitute.For<ITenantsAccessor>();
        e.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<(Guid, string)>>([]));
        return e;
    }

    private sealed class TenantScopedDbContextFactory(
        DbContextOptions<WebhooksHostDbContext> options,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter) : IDbContextFactory<WebhooksHostDbContext>
    {
        public WebhooksHostDbContext CreateDbContext() => new(options, currentTenant, dataFilter);
    }
}
