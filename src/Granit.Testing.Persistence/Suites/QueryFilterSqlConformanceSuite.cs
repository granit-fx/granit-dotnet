using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Testing.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Testing.Persistence.Suites;

/// <summary>
/// Proves the non-tenant named query filters (soft-delete, active) hold in real generated
/// SQL — the class of behavior the InMemory provider silently fakes.
/// </summary>
public abstract class QueryFilterSqlConformanceSuite(IRelationalConformanceFixture fixture)
{
    [Fact]
    public async Task Soft_deleted_row_survives_physically_but_is_filtered()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        harness.CurrentTenant.Id = Guid.CreateVersion7();
        string label = $"sd-{Guid.CreateVersion7():N}";

        Guid id;
        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            ConformanceOrder order = new() { Label = label };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            id = order.Id;

            db.Orders.Remove(order);
            await db.SaveChangesAsync();
        }

        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            (await db.Orders.AnyAsync(o => o.Id == id))
                .ShouldBeFalse($"{fixture.ProviderName}: standard queries must exclude soft-deleted rows");

            ConformanceOrder? tombstone = await db.Orders
                .IgnoreQueryFilters([GranitFilterNames.SoftDelete])
                .FirstOrDefaultAsync(o => o.Id == id);
            tombstone.ShouldNotBeNull(
                $"{fixture.ProviderName}: the row must physically survive (UPDATE, not DELETE)");
            tombstone.IsDeleted.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Inactive_rows_are_filtered_until_bypassed_per_query()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        string label = $"act-{Guid.CreateVersion7():N}";

        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            db.Toggles.Add(new ConformanceToggle { Label = label, Activated = true });
            db.Toggles.Add(new ConformanceToggle { Label = label, Activated = false });
            await db.SaveChangesAsync();
        }

        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            (await db.Toggles.CountAsync(t => t.Label == label)).ShouldBe(1);

            (await db.Toggles
                .IgnoreQueryFilters([GranitFilterNames.Active])
                .CountAsync(t => t.Label == label))
                .ShouldBe(2, $"{fixture.ProviderName}: per-query bypass must reveal inactive rows");
        }
    }

    [Fact(Skip = "Repro of #3174: IDataFilter.Disable<T>() is folded into the cached plan for "
        + "proxy-backed filters on relational providers — the bypass never reaches SQL. "
        + "Un-skip when the flags become GranitDbContext instance members.")]
    public async Task Flow_scoped_disable_reveals_inactive_rows()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        string label = $"actflow-{Guid.CreateVersion7():N}";

        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            db.Toggles.Add(new ConformanceToggle { Label = label, Activated = true });
            db.Toggles.Add(new ConformanceToggle { Label = label, Activated = false });
            await db.SaveChangesAsync();
        }

        using (harness.DataFilter.Disable<IActive>())
        {
            await using ConformanceDbContext db = await harness.CreateContextAsync();
            (await db.Toggles.CountAsync(t => t.Label == label)).ShouldBe(2);
        }
    }
}
