using Granit.Domain;
using Granit.Testing.Persistence.Domain;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.Testing.Persistence.Suites;

/// <summary>
/// Proves tenant isolation on a real database: tenant A never reads tenant B's rows, an
/// absent tenant context fails CLOSED to the host partition, and the tenant filter is
/// parameterised SQL (never an inlined constant — the historical cross-request leak shape).
/// </summary>
public abstract class TenantIsolationConformanceSuite(IRelationalConformanceFixture fixture)
{
    [Fact]
    public async Task Rows_of_tenant_A_are_invisible_to_tenant_B()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        var tenantA = Guid.CreateVersion7();
        var tenantB = Guid.CreateVersion7();
        string label = $"iso-{Guid.CreateVersion7():N}";

        harness.CurrentTenant.Id = tenantA;
        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            db.Orders.Add(new ConformanceOrder { Label = label });
            await db.SaveChangesAsync();
        }

        harness.CurrentTenant.Id = tenantB;
        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            (await db.Orders.Where(o => o.Label == label).ToListAsync())
                .ShouldBeEmpty($"{fixture.ProviderName}: tenant B must not see tenant A's rows");
        }

        harness.CurrentTenant.Id = tenantA;
        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            (await db.Orders.Where(o => o.Label == label).ToListAsync()).ShouldHaveSingleItem();
        }
    }

    [Fact]
    public async Task Absent_tenant_context_fails_closed_to_host_partition()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        var tenantA = Guid.CreateVersion7();
        string label = $"host-{Guid.CreateVersion7():N}";

        harness.CurrentTenant.Id = tenantA;
        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            db.Orders.Add(new ConformanceOrder { Label = label });
            await db.SaveChangesAsync();
        }

        // Host row (TenantId == null) written without an active tenant.
        harness.CurrentTenant.Id = null;
        await using (ConformanceDbContext db = await harness.CreateContextAsync())
        {
            db.Orders.Add(new ConformanceOrder { Label = label });
            await db.SaveChangesAsync();

            // Unsignaled absence of a tenant: only the host partition is visible.
            List<ConformanceOrder> visible = await db.Orders.Where(o => o.Label == label).ToListAsync();
            visible.ShouldHaveSingleItem(
                $"{fixture.ProviderName}: an absent tenant context must fail closed to TenantId == null");
            visible[0].TenantId.ShouldBeNull();
        }
    }

    [Fact]
    public async Task Disabling_the_multitenant_filter_reveals_all_tenants()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        string label = $"all-{Guid.CreateVersion7():N}";

        foreach (Guid tenant in (Guid[])[Guid.CreateVersion7(), Guid.CreateVersion7()])
        {
            harness.CurrentTenant.Id = tenant;
            await using ConformanceDbContext db = await harness.CreateContextAsync();
            db.Orders.Add(new ConformanceOrder { Label = label });
            await db.SaveChangesAsync();
        }

        harness.CurrentTenant.Id = Guid.CreateVersion7();
        using (harness.DataFilter.Disable<IMultiTenant>())
        {
            await using ConformanceDbContext db = await harness.CreateContextAsync();
            (await db.Orders.CountAsync(o => o.Label == label)).ShouldBe(2);
        }
    }

    [Fact]
    public async Task Tenant_filter_is_parameterised_not_inlined()
    {
        await using ConformanceHarness harness = await ConformanceHarness.CreateAsync(fixture);
        var tenant = Guid.CreateVersion7();
        harness.CurrentTenant.Id = tenant;

        await using ConformanceDbContext db = await harness.CreateContextAsync();

        // ToQueryString prefixes the SQL with parameter declarations — `-- @param='value'`
        // comments on PostgreSQL, `DECLARE @param type = 'value';` statements on SQL Server.
        // The assertion targets the statement body only (a parameter VALUE in the preamble is
        // fine — a literal in the WHERE clause is the leak).
        string sql = string.Join('\n', db.Orders.ToQueryString()
            .Split('\n')
            .Where(line => !line.StartsWith("--", StringComparison.Ordinal)
                && !line.StartsWith("DECLARE ", StringComparison.OrdinalIgnoreCase)));

        // The tenant value must reach SQL as a re-bound parameter (@ef_filter__*). An inlined
        // literal means the first request's tenant is frozen into the cached plan — the exact
        // cross-request leak shape guarded by MultiTenantFilterParameterizationReproTests.
        sql.ShouldContain("ef_filter",
            customMessage: $"{fixture.ProviderName}: tenant filter must be a query parameter");
        sql.ShouldNotContain(tenant.ToString(),
            customMessage: $"{fixture.ProviderName}: tenant id must never be inlined into SQL");
    }
}
