using Granit.DataFiltering;
using Granit.Domain;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly DbContextOptions<WebhooksDbContext> _options;
    private readonly DataFilter _filter = new();

    public EfWebhookSubscriptionQueryableSourceTests()
    {
        _options = new DbContextOptionsBuilder<WebhooksDbContext>()
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
        var sut = new EfWebhookSubscriptionQueryableSource(
            new TenantScopedDbContextFactory(_options, tenantA, _filter),
            ScopeFor(tenantA));

        // Act
        WebhookSubscription[] result = [.. sut.GetQueryable()];

        // Assert — tenant B's row is gone; tenant A's row remains.
        result.ShouldHaveSingleItem();
        result[0].TenantId.ShouldBe(_tenantA);
    }

    [Fact]
    public async Task GetQueryable_SignaledHostContext_ReturnsAllTenants()
    {
        // Arrange
        await SeedAcrossTenantsAsync(
            CreateSubscription("doc.uploaded", _tenantA),
            CreateSubscription("doc.uploaded", _tenantB),
            CreateSubscription("doc.uploaded", tenantId: null));

        // No tenant + signaled .AllowHostAccess() — the authorized cross-tenant path bypasses the
        // MultiTenant filter and returns every subscription (VULN-001).
        ICurrentTenant host = TenantStub(tenantId: null, isAvailable: false);
        var sut = new EfWebhookSubscriptionQueryableSource(
            new TenantScopedDbContextFactory(_options, host, _filter),
            ScopeFor(host, hostAccess: true));

        // Act
        WebhookSubscription[] result = [.. sut.GetQueryable()];

        // Assert — all three rows visible to the signaled host context.
        result.Length.ShouldBe(3);
    }

    [Fact]
    public async Task GetQueryable_UnsignaledNoTenant_FailsClosed_ReturnsOnlyHostPartition()
    {
        // VULN-001: an unsignaled absent tenant must NOT leak foreign-tenant rows. Only the
        // platform (null-tenant) subscription is visible; tenant A/B rows stay hidden.
        await SeedAcrossTenantsAsync(
            CreateSubscription("doc.uploaded", _tenantA),
            CreateSubscription("doc.uploaded", _tenantB),
            CreateSubscription("doc.uploaded", tenantId: null));

        ICurrentTenant host = TenantStub(tenantId: null, isAvailable: false);
        var sut = new EfWebhookSubscriptionQueryableSource(
            new TenantScopedDbContextFactory(_options, host, _filter),
            ScopeFor(host));

        WebhookSubscription[] result = [.. sut.GetQueryable()];

        result.ShouldHaveSingleItem();
        result[0].TenantId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task SeedAcrossTenantsAsync(params WebhookSubscription[] subscriptions)
    {
        using IDisposable _ = _filter.Disable<IMultiTenant>();
        await using WebhooksDbContext context = new(_options, TenantStub(tenantId: null, isAvailable: false), _filter);
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

    // Real ITenantQueryScope (from AddGranitPersistence) wired to the given tenant and optional
    // host-access signal — the same fail-closed decision the production QueryEngine path uses.
    private static ITenantQueryScope ScopeFor(ICurrentTenant tenant, bool hostAccess = false)
    {
        ServiceCollection services = new();
        services.AddMetrics();
        services.AddLogging();
        services.AddSingleton(tenant);
        if (hostAccess)
        {
            IHostAccessContext host = Substitute.For<IHostAccessContext>();
            host.IsHostAccess.Returns(true);
            services.AddSingleton(host);
        }

        services.AddGranitPersistence();
        return services.BuildServiceProvider().GetRequiredService<ITenantQueryScope>();
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

    private sealed class TenantScopedDbContextFactory(
        DbContextOptions<WebhooksDbContext> options,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter) : IDbContextFactory<WebhooksDbContext>
    {
        public WebhooksDbContext CreateDbContext() => new(options, currentTenant, dataFilter);
    }
}
