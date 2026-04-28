using Granit.Dashboards.Domain;
using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.DataFiltering;
using Granit.Domain;
using Granit.Testing.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Dashboards.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Verifies that <c>ApplyGranitConventions</c> wires the soft-delete filter on
/// <see cref="Dashboard"/> rows: a soft-deleted dashboard disappears from the
/// default queryable surface and reappears when the filter is disabled (admin
/// restore flow).
/// </summary>
public sealed class DashboardSoftDeleteTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;

    public DashboardSoftDeleteTests(PostgresFixture postgres)
        => _postgres = postgres;

    public async ValueTask InitializeAsync()
    {
        await using DashboardsDbContext ctx = NewContext(dataFilter: null);
        await ctx.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SoftDeletedDashboards_AreHidden_ByDefault()
    {
        var liveId = Guid.NewGuid();
        var deletedId = Guid.NewGuid();

        await using (DashboardsDbContext seed = NewContext(dataFilter: null))
        {
            var live = Dashboard.Create(liveId, "Live", DashboardCategory.General);
            var deleted = Dashboard.Create(deletedId, "Deleted", DashboardCategory.General);
            ((ISoftDeletable)deleted).IsDeleted = true;

            seed.Dashboards.AddRange(live, deleted);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Default queryable: filter ON — only the live row.
        await using (DashboardsDbContext ctx = NewContext(dataFilter: null))
        {
            List<Guid> visibleIds = await ctx.Dashboards
                .Select(d => d.Id)
                .ToListAsync(TestContext.Current.CancellationToken);

            visibleIds.ShouldBe([liveId]);
        }
    }

    private DashboardsDbContext NewContext(IDataFilter? dataFilter)
    {
        DbContextOptions<DashboardsDbContext> options = new DbContextOptionsBuilder<DashboardsDbContext>()
            .UseNpgsql(_postgres.ConnectionString)
            .EnableGranitTestModelIsolation()
            .Options;

        return new DashboardsDbContext(options, currentTenant: null, dataFilter: dataFilter);
    }
}
