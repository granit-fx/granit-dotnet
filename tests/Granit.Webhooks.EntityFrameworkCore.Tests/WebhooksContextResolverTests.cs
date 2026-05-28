using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.MultiTenancy;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

/// <summary>
/// Pins the dispatch contract of <see cref="WebhooksContextResolver"/> — the central
/// piece that every store relies on to route reads and writes per scope across
/// <c>Shared</c> and <c>Segregated</c> storage modes.
/// </summary>
public sealed class WebhooksContextResolverTests
{
    [Fact]
    public async Task Shared_OpenForScope_AlwaysOpensHostFactory()
    {
        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory();
        WebhooksContextResolver sut = new(DualScopeStorageMode.Shared, hostFactory);

        await using IWebhooksDbContext ctx = await sut.OpenForScopeAsync(tenantId: Guid.NewGuid(), TestContext.Current.CancellationToken);
        await using IWebhooksDbContext ctx2 = await sut.OpenForScopeAsync(tenantId: null, TestContext.Current.CancellationToken);

        _ = hostFactory.Received(2).CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Shared_OpenAll_ReturnsSingleHostContext()
    {
        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory();
        WebhooksContextResolver sut = new(DualScopeStorageMode.Shared, hostFactory);

        IReadOnlyList<IWebhooksDbContext> contexts = await sut.OpenAllAsync(TestContext.Current.CancellationToken);
        try
        {
            contexts.Count.ShouldBe(1);
        }
        finally
        {
            foreach (IWebhooksDbContext c in contexts) { await c.DisposeAsync(); }
        }
    }

    [Fact]
    public async Task Segregated_OpenForScope_NullTenantId_OpensHost()
    {
        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory();
        IDbContextFactory<WebhooksTenantDbContext> tenantFactory = StubTenantFactory();
        WebhooksContextResolver sut = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        await using IWebhooksDbContext ctx = await sut.OpenForScopeAsync(tenantId: null, TestContext.Current.CancellationToken);

        _ = hostFactory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
        _ = tenantFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Segregated_OpenForScope_TenantId_OpensTenant()
    {
        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory();
        IDbContextFactory<WebhooksTenantDbContext> tenantFactory = StubTenantFactory();
        WebhooksContextResolver sut = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        await using IWebhooksDbContext ctx = await sut.OpenForScopeAsync(tenantId: Guid.NewGuid(), TestContext.Current.CancellationToken);

        _ = hostFactory.DidNotReceive().CreateDbContextAsync(Arg.Any<CancellationToken>());
        _ = tenantFactory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Segregated_OpenAll_ReturnsBothContexts_HostThenTenant()
    {
        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory();
        IDbContextFactory<WebhooksTenantDbContext> tenantFactory = StubTenantFactory();
        WebhooksContextResolver sut = new(DualScopeStorageMode.Segregated, hostFactory, tenantFactory);

        IReadOnlyList<IWebhooksDbContext> contexts = await sut.OpenAllAsync(TestContext.Current.CancellationToken);
        try
        {
            contexts.Count.ShouldBe(2);
            _ = hostFactory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
            _ = tenantFactory.Received(1).CreateDbContextAsync(Arg.Any<CancellationToken>());
        }
        finally
        {
            foreach (IWebhooksDbContext c in contexts) { await c.DisposeAsync(); }
        }
    }

    [Fact]
    public async Task Segregated_OpenForScope_TenantWithoutFactory_Throws()
    {
        IDbContextFactory<WebhooksHostDbContext> hostFactory = StubHostFactory();
        WebhooksContextResolver sut = new(
            DualScopeStorageMode.Segregated,
            hostFactory,
            tenantFactory: null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            async () => await sut.OpenForScopeAsync(tenantId: Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Segregated");
        ex.Message.ShouldContain("WebhooksTenantDbContext");
    }

    private static IDbContextFactory<WebhooksHostDbContext> StubHostFactory()
    {
        IDbContextFactory<WebhooksHostDbContext> factory = Substitute.For<IDbContextFactory<WebhooksHostDbContext>>();
        DbContextOptions<WebhooksHostDbContext> opts = new DbContextOptionsBuilder<WebhooksHostDbContext>()
            .UseInMemoryDatabase("resolver-host-" + Guid.NewGuid())
            .Options;
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new WebhooksHostDbContext(opts, GranitDesignTime.CurrentTenant)));
        return factory;
    }

    private static IDbContextFactory<WebhooksTenantDbContext> StubTenantFactory()
    {
        IDbContextFactory<WebhooksTenantDbContext> factory = Substitute.For<IDbContextFactory<WebhooksTenantDbContext>>();
        DbContextOptions<WebhooksTenantDbContext> opts = new DbContextOptionsBuilder<WebhooksTenantDbContext>()
            .UseInMemoryDatabase("resolver-tenant-" + Guid.NewGuid())
            .Options;
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(new WebhooksTenantDbContext(opts, GranitDesignTime.CurrentTenant)));
        return factory;
    }
}
