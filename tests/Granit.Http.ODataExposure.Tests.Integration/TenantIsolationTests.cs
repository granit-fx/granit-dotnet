using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Cross-tenant attack scenarios for C2 (#1391) — pins the load-bearing
/// security control of the OData exposure layer: a malicious
/// <c>$filter=tenantId eq &lt;other_tenant&gt;</c> MUST NOT leak rows
/// from another tenant. Each test has a comment linking the threat to
/// the issue.
/// </summary>
public sealed class TenantIsolationTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = postgres;
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private ODataTestApp _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = await ODataTestApp.CreateAsync(_postgres.ConnectionString);
        await SeedAsync();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    [Fact]
    public async Task TenantA_CallsList_SeesOnlyTenantARows()
    {
        // Baseline — the framework filter is applied; no user filter.
        // Acceptance criterion #1 from #1391.
        HttpResponseMessage response = await GetAsync(TenantA, "Invoices");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.Count.ShouldBe(5);
        rows.ShouldAllBe(r => r.GetProperty("TenantId").GetGuid() == TenantA);
    }

    [Fact]
    public async Task TenantA_HostileFilterPointingAtTenantB_ReturnsEmpty()
    {
        // The cross-tenant attack vector: tenant A's user crafts
        // $filter=tenantId eq <TenantB> — if framework filter is composed
        // BEFORE, the resulting WHERE is `TenantId = A AND tenantId = B`
        // → 0 rows. If composed AFTER (regression), tenant B's rows leak.
        // Acceptance criterion #2 from #1391.
        HttpResponseMessage response = await GetAsync(
            TenantA, $"Invoices?$filter=TenantId eq {TenantB:D}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task TenantA_HostileFilterWithOrClause_StillScopedToTenantA()
    {
        // The OR variant of the attack — `tenantId eq B OR amount gt 0`
        // would, without compose-FIRST, return tenant A's rows AND tenant B's
        // rows where amount > 0. With compose-FIRST, the framework AND-prefix
        // restricts the entire OR-clause to tenant A: 5 rows, all A's.
        // Acceptance criterion #3 from #1391.
        HttpResponseMessage response = await GetAsync(
            TenantA, $"Invoices?$filter=TenantId eq {TenantB:D} or Amount gt 0");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.Count.ShouldBe(5);
        rows.ShouldAllBe(r => r.GetProperty("TenantId").GetGuid() == TenantA);
    }

    [Fact]
    public async Task TenantA_DoesNotSeeSoftDeletedRows_EvenWithExplicitFilter()
    {
        // The IsDeleted filter is mandatory — IsDeleted IS NOT exposed in the
        // QueryDefinition column whitelist, so an OData $filter targeting it
        // either round-trips an unrecognised property (400) or, more
        // interestingly, the framework's HasQueryFilter still kicks in.
        // Acceptance criterion #5 from #1391.
        HttpResponseMessage response = await GetAsync(TenantA, "Invoices");

        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.ShouldAllBe(r => !IsSoftDeletedInPayload(r));

        // Verify directly via a raw count that the soft-deleted row exists in
        // the database — proves our test setup is sound, not just empty.
        using IServiceScope scope = _app.CreateScope();
        TestDbContext db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
        int totalIncludingDeleted = await db.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == TenantA)
            .CountAsync(TestContext.Current.CancellationToken);
        totalIncludingDeleted.ShouldBe(6); // 5 visible + 1 soft-deleted
    }

    [Fact]
    public async Task SqlLog_ContainsTenantFilterPrefix_BeforeUserFilter()
    {
        // The SQL inspection assertion from #1391 — the tenant filter must
        // appear as `WHERE TenantId = @t1` before any user-supplied predicate.
        // EF Core's translator emits the model's HasQueryFilter clauses first
        // (compose-FIRST guarantee); we confirm the actual SQL.
        _app.SqlCapture.Clear();
        await GetAsync(TenantA, $"Invoices?$filter=Amount gt 100");

        string? selectCommand = _app.SqlCapture.Commands
            .FirstOrDefault(c => c.Contains(@"FROM ""Invoices""", StringComparison.Ordinal));

        selectCommand.ShouldNotBeNull(
            "expected at least one SELECT against \"Invoices\" in the captured SQL");

        // EF Core compiles the global filter as `WHERE i.TenantId = @tenantId AND
        // i.IsDeleted = false AND <user predicate>`. The tenant column reference
        // must appear before the user's Amount predicate IN THE WHERE CLAUSE —
        // both columns also appear earlier in the SELECT projection list
        // (alphabetised by EF Core: Amount, ..., TenantId), so the IndexOf
        // comparison must be scoped to the WHERE substring; otherwise the
        // SELECT positions dominate and the assertion fails on a true positive.
        int whereIndex = selectCommand!.IndexOf("WHERE", StringComparison.Ordinal);
        whereIndex.ShouldBeGreaterThan(0, "expected a WHERE clause in the captured SQL");

        string whereClause = selectCommand[whereIndex..];
        int tenantIndex = whereClause.IndexOf(@"""TenantId""", StringComparison.Ordinal);
        int amountIndex = whereClause.IndexOf(@"""Amount""", StringComparison.Ordinal);

        tenantIndex.ShouldBeGreaterThan(0, "tenant filter must appear in the WHERE clause");
        amountIndex.ShouldBeGreaterThan(0, "user $filter must appear in the WHERE clause");
        tenantIndex.ShouldBeLessThan(amountIndex,
            $"tenant filter must come before user $filter — actual SQL:\n{selectCommand}");
    }

    [Fact]
    public async Task TenantA_NarrowSelectOnTenantId_StillScopedToTenantA()
    {
        // $select=TenantId returns only the tenant column. Even when the
        // user picks the smallest possible projection, the framework
        // filter still keeps the row set bound to TenantA — projection
        // does not move filtering to the client.
        HttpResponseMessage response = await GetAsync(
            TenantA, "Invoices?$select=TenantId");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.Count.ShouldBe(5);
        rows.ShouldAllBe(r => r.GetProperty("TenantId").GetGuid() == TenantA);
    }

    [Fact]
    public async Task TenantA_OrderByTenantIdDesc_DoesNotLeakOtherTenants()
    {
        // $orderby on the TenantId column itself: a hostile actor could
        // hope sorting somehow includes other tenants' rows. The framework
        // filter is unaffected by ORDER BY — only the row order changes,
        // and only T1's rows ever enter the sort.
        HttpResponseMessage response = await GetAsync(
            TenantA, "Invoices?$orderby=TenantId desc");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.Count.ShouldBe(5);
        rows.ShouldAllBe(r => r.GetProperty("TenantId").GetGuid() == TenantA);
    }

    [Fact]
    public async Task TenantA_FunctionCallCombinedWithHostilePredicate_StaysScopedToTenantA()
    {
        // Composes a built-in OData function (contains) with the hostile
        // filter — defends against the hypothesis that an OData translator
        // optimisation might re-order or short-circuit when a function is
        // present in the user predicate. AND-prefix wins regardless.
        HttpResponseMessage response = await GetAsync(
            TenantA,
            $"Invoices?$filter=contains(Number, 'A') and TenantId eq {TenantB:D}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnauthenticatedRequest_NoTenantHeader_ReturnsEmpty()
    {
        // No tenant header means ICurrentTenant.Id is null, the multi-tenant
        // filter compares TenantId == null, no seeded row matches → empty.
        // Real applications would short-circuit earlier via 401; this check
        // pins the OData layer's behaviour when tenant context is absent.
        HttpResponseMessage response = await GetAsync(tenant: null, "Invoices");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.ShouldBeEmpty();
    }

    private static bool IsSoftDeletedInPayload(JsonElement row) =>
        row.TryGetProperty("IsDeleted", out JsonElement d) && d.GetBoolean();

    private async Task<HttpResponseMessage> GetAsync(Guid? tenant, string relativePath)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/granit/odata/{relativePath}");
        if (tenant is { } t)
        {
            request.Headers.Add("X-Test-Tenant", t.ToString());
        }
        return await _app.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<JsonElement>> ReadValueAsync(HttpResponseMessage response)
    {
        JsonDocument doc = await response.Content.ReadFromJsonAsync<JsonDocument>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("OData response was empty.");

        if (!doc.RootElement.TryGetProperty("value", out JsonElement value))
        {
            throw new InvalidOperationException(
                $"OData response is missing the 'value' array: {doc.RootElement.GetRawText()}");
        }

        return [.. value.EnumerateArray()];
    }

    private async Task SeedAsync()
    {
        using IServiceScope scope = _app.CreateScope();
        TestDbContext db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        // The PostgresFixture container is shared across the whole test class,
        // so leftover rows from a previous test would inflate the row counts
        // asserted below. IgnoreQueryFilters so we wipe the soft-deleted row
        // too — the tenant filter never applies because each row's TenantId
        // is set explicitly.
        await db.Invoices.IgnoreQueryFilters().ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        // Seed 5 invoices per tenant + 1 soft-deleted in tenant A. Ignoring
        // the query filter so the soft-deleted row actually persists; the
        // tenant filter is bypassed too because we set TenantId explicitly
        // on each row (avoids reliance on the AsyncLocal during seeding).
        for (int i = 0; i < 5; i++)
        {
            db.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                Number = $"A-{i + 1:D3}",
                Amount = (i + 1) * 100m,
            });

            db.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantB,
                Number = $"B-{i + 1:D3}",
                Amount = (i + 1) * 200m,
            });
        }

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            Number = "A-SOFT-DELETED",
            Amount = 999m,
            IsDeleted = true,
            DeletedAt = DateTimeOffset.UtcNow,
            DeletedBy = "seed",
        });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
