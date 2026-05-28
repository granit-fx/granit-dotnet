using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Webhooks.EntityFrameworkCore.Extensions;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.EntityFrameworkCore.Tests;

/// <summary>
/// Verifies that <see cref="WebhooksDualScopeIntegrationValidator"/> fails fast at
/// host startup when a tenant-isolated DbContext folds the Webhooks model — the
/// integration mistake that produced the 42P01 in the showcase #2021 follow-up.
/// </summary>
public sealed class WebhooksDualScopeIntegrationValidatorTests
{
    [Fact]
    public async Task StartAsync_NoIsolatedMarkers_DoesNotThrow()
    {
        ServiceProvider sp = new ServiceCollection().BuildServiceProvider();
        var sut = new WebhooksDualScopeIntegrationValidator(
            markers: [],
            serviceProvider: sp,
            logger: NullLogger<WebhooksDualScopeIntegrationValidator>.Instance);

        await sut.StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_IsolatedDbContextWithoutWebhooks_DoesNotThrow()
    {
        ServiceProvider sp = BuildProvider<CleanIsolatedDbContext>();
        var sut = new WebhooksDualScopeIntegrationValidator(
            markers: sp.GetServices<IsolatedDbContextMarker>(),
            serviceProvider: sp,
            logger: NullLogger<WebhooksDualScopeIntegrationValidator>.Instance);

        await sut.StartAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task StartAsync_IsolatedDbContextFoldsWebhooks_Throws()
    {
        ServiceProvider sp = BuildProvider<OffendingIsolatedDbContext>();
        var sut = new WebhooksDualScopeIntegrationValidator(
            markers: sp.GetServices<IsolatedDbContextMarker>(),
            serviceProvider: sp,
            logger: NullLogger<WebhooksDualScopeIntegrationValidator>.Instance);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => sut.StartAsync(TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("dual-scope");
        ex.Message.ShouldContain(nameof(OffendingIsolatedDbContext));
        ex.Message.ShouldContain("WebhookSubscription");
    }

    private static ServiceProvider BuildProvider<TContext>()
        where TContext : DbContext
    {
        IServiceCollection services = new ServiceCollection();

        // Minimal isolated registration: a SharedDatabase keyed factory the validator
        // can resolve, plus the marker the validator enumerates. We bypass
        // AddGranitIsolatedDbContext so the test stays a true unit test (no
        // configuration binding, no ValidateOnStart side effects).
        services.AddKeyedScoped<IDbContextFactory<TContext>>(
            TenantIsolationStrategy.SharedDatabase,
            (sp, _) => new InMemoryDbContextFactory<TContext>());

        services.AddSingleton(new IsolatedDbContextMarker(
            typeof(TContext),
            new HashSet<TenantIsolationStrategy> { TenantIsolationStrategy.SharedDatabase }));

        return services.BuildServiceProvider();
    }

    private sealed class InMemoryDbContextFactory<TContext> : IDbContextFactory<TContext>
        where TContext : DbContext
    {
        public TContext CreateDbContext()
        {
            DbContextOptionsBuilder<TContext> builder = new DbContextOptionsBuilder<TContext>()
                .UseInMemoryDatabase($"validator-{Guid.NewGuid()}");
            return (TContext)Activator.CreateInstance(
                typeof(TContext),
                builder.Options,
                GranitDesignTime.CurrentTenant,
                NullDataFilter.Instance)!;
        }

        public Task<TContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    // -------------------------------------------------------------------------
    // Test DbContexts — both inherit from GranitDbContext so the SingleValueObject
    // converters from ApplyGranitConventions are wired and the Webhooks aggregates
    // can compile their model under the InMemory provider.
    // -------------------------------------------------------------------------

    internal sealed class CleanIsolatedDbContext(
        DbContextOptions<CleanIsolatedDbContext> options,
        ICurrentTenant currentTenant,
        IDataFilter? dataFilter = null) : GranitDbContext(options, currentTenant, dataFilter)
    {
        // No Webhooks fold — validator should pass.
        protected override void OnGranitModelCreating(ModelBuilder modelBuilder) { }
    }

    internal sealed class OffendingIsolatedDbContext(
        DbContextOptions<OffendingIsolatedDbContext> options,
        ICurrentTenant currentTenant,
        IDataFilter? dataFilter = null) : GranitDbContext(options, currentTenant, dataFilter)
    {
        // The integration mistake: folding Webhooks into an isolated DbContext.
        protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.ConfigureWebhooksModule();
    }
}
