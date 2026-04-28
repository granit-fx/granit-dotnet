using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Granit.Testing.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Verifies that <c>ApplyGranitConventions</c> wires the multi-tenant filter on
/// <see cref="Dashboard"/> rows: a tenant cannot see another tenant's dashboards
/// through the standard <see cref="DashboardsDbContext"/> queryable surface.
/// Postgres-only — SQLite tolerates a few corner cases that Postgres rejects.
/// </summary>
public sealed class DashboardTenantIsolationTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public DashboardTenantIsolationTests(PostgresFixture postgres)
        => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        // Testcontainers boots a fresh database per fixture instance — only need to
        // create the schema. EnsureDeletedAsync would fail with PostgresException
        // 55006 ("cannot drop the currently open database") since the connection
        // holds it open.
        await using DashboardsDbContext ctx = NewContext(currentTenantId: null);
        await ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Dashboards_AreFilteredToCurrentTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Seed without a current tenant so the filter does not interfere.
        await using (DashboardsDbContext seed = NewContext(currentTenantId: null))
        {
            seed.Dashboards.AddRange(
                Dashboard.Create(Guid.NewGuid(), "A1", DashboardCategory.Finance, tenantId: tenantA),
                Dashboard.Create(Guid.NewGuid(), "A2", DashboardCategory.Operations, tenantId: tenantA),
                Dashboard.Create(Guid.NewGuid(), "B1", DashboardCategory.Finance, tenantId: tenantB));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Query as tenant A — should see only A's dashboards.
        await using (DashboardsDbContext ctx = NewContext(currentTenantId: tenantA))
        {
            List<string> names = await ctx.Dashboards
                .OrderBy(d => d.Name)
                .Select(d => d.Name)
                .ToListAsync(TestContext.Current.CancellationToken);

            names.ShouldBe(["A1", "A2"]);
        }

        // Query as tenant B — should see only B's dashboards.
        await using (DashboardsDbContext ctx = NewContext(currentTenantId: tenantB))
        {
            List<string> names = await ctx.Dashboards
                .Select(d => d.Name)
                .ToListAsync(TestContext.Current.CancellationToken);

            names.ShouldBe(["B1"]);
        }
    }

    private DashboardsDbContext NewContext(Guid? currentTenantId)
    {
        // EnableGranitTestModelIsolation defeats EF Core's process-wide model cache:
        // without it, the first context's tenant substitute is captured by reference
        // in the named multi-tenant query filter and reused by every subsequent context.
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .EnableGranitTestModelIsolation()
            .Options;

        ICurrentTenant tenant = Substitute.For<ICurrentTenant>();
        tenant.Id.Returns(currentTenantId);
        tenant.IsAvailable.Returns(currentTenantId.HasValue);

        return new DashboardsDbContext(options, currentTenant: tenant);
    }
}
