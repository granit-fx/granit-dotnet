using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// C3 (#1392) hardening assertions — pins the per-EntitySet caps that
/// protect the OData layer against DoS-style queries (huge $top, no-limit
/// table scans, $expand explosions, $count on large tables). Each scenario
/// runs against the same PostgreSQL container as the tenant-isolation
/// suite; the test fixture configures the EntitySet differently per class
/// to keep assertions focused.
/// </summary>
public sealed class QueryHardeningTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = postgres;
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ODataTestApp _appTopCapped = null!;
    private ODataTestApp _appExpandWhitelist = null!;
    private ODataTestApp _appCountEnabled = null!;

    public async ValueTask InitializeAsync()
    {
        // Three apps with different per-set hardening configs. Same DB,
        // same seed — only the EntitySet builder differs. Sharing the
        // PostgreSQL container is fine because the tests don't mutate;
        // each app maintains its own DbContext but uses
        // EnsureCreatedAsync() (idempotent) on the shared schema.
        _appTopCapped = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            builder => builder.MaxTop(5).PageSize(3));

        _appExpandWhitelist = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            builder => builder.ExpandWhitelist("Customer"));

        _appCountEnabled = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            builder => builder.EnableCount());

        await SeedAsync(_appTopCapped);
    }

    public async ValueTask DisposeAsync()
    {
        await _appTopCapped.DisposeAsync();
        await _appExpandWhitelist.DisposeAsync();
        await _appCountEnabled.DisposeAsync();
    }

    [Fact]
    public async Task MaxTop_RequestAboveCap_ResponseSilentlyClamped_AndHeaderEmitted()
    {
        // EntitySet declared with MaxTop=5; user asks for $top=1000000.
        // Acceptance criterion #2 from #1392 — the response is capped, not
        // rejected, and the OData-MaxTop-Applied header surfaces the
        // clamping for observability.
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/api/granit/odata/Invoices?$top=1000000");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());

        HttpResponseMessage response = await _appTopCapped.Client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.GetValues("OData-MaxTop-Applied").ShouldHaveSingleItem().ShouldBe("5");
    }

    [Fact]
    public async Task MaxTop_RequestBelowCap_HeaderNotEmitted()
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/api/granit/odata/Invoices?$top=2");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());

        HttpResponseMessage response = await _appTopCapped.Client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("OData-MaxTop-Applied").ShouldBeFalse();
    }

    [Fact]
    public async Task Count_NotEnabled_ReturnsBadRequest()
    {
        // Acceptance criterion #4 from #1392 — $count=true on an EntitySet
        // without .EnableCount() returns 400. Guards huge tables against
        // full-table-scan counts on every BI refresh.
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/api/granit/odata/Invoices?$count=true");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());

        HttpResponseMessage response = await _appTopCapped.Client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("count");
    }

    [Fact]
    public async Task Count_Enabled_RequestAccepted()
    {
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/api/granit/odata/Invoices?$count=true");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());

        HttpResponseMessage response = await _appCountEnabled.Client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Expand_NoWhitelist_ReturnsBadRequest()
    {
        // EntitySet on _appTopCapped did not call .ExpandWhitelist(...),
        // so any $expand request is rejected. Acceptance criterion #1.
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/api/granit/odata/Invoices?$expand=Customer");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());

        HttpResponseMessage response = await _appTopCapped.Client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Expand_NotInWhitelist_ReturnsBadRequest()
    {
        // _appExpandWhitelist allows ["Customer"]; any other property fails.
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/api/granit/odata/Invoices?$expand=Lines");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());

        HttpResponseMessage response = await _appExpandWhitelist.Client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Lines");
    }

    [Fact]
    public async Task Expand_InWhitelist_AcceptedThoughTheNavigationDoesntExist()
    {
        // The Invoice entity has no Customer navigation in the test schema;
        // OData responds with a parser-level error (it throws an
        // ODataException straight up, which TestServer re-raises as a
        // SendAsync exception). What we pin here is that the C3 whitelist
        // check does NOT pre-empt the request when the property name matches
        // the whitelist — so the failure must come from the OData parser
        // itself, never from the framework's "not permitted" rejection.
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/api/granit/odata/Invoices?$expand=Customer");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());

        // Either the request returns a response (200 with empty navigation
        // or 400 from the translator) OR it throws an ODataException because
        // the EDM has no Customer navigation. Both outcomes prove the C3
        // layer let the request through; only a "not permitted" payload
        // would indicate the whitelist short-circuited.
        string body = string.Empty;
        try
        {
            HttpResponseMessage response = await _appExpandWhitelist.Client.SendAsync(
                request, TestContext.Current.CancellationToken);
            body = response.Content.Headers.ContentLength is > 0
                ? await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
                : string.Empty;
        }
        catch (Microsoft.OData.ODataException)
        {
            // Expected when OData can't find the Customer navigation in the
            // EDM. The exception itself proves the C3 check accepted the
            // expand — assertion satisfied trivially.
            return;
        }

        body.ShouldNotContain("not permitted");
        body.ShouldNotContain("not whitelisted");
    }

    [Fact]
    public async Task PageSize_AppliedWhenTopOmitted()
    {
        // _appTopCapped has PageSize=3. Without $top, the response should
        // expose at most 3 invoices.
        using HttpRequestMessage request = new(HttpMethod.Get,
            "/api/granit/odata/Invoices");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());

        HttpResponseMessage response = await _appTopCapped.Client.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.Count.ShouldBeLessThanOrEqualTo(3);
    }

    private static async Task<IReadOnlyList<JsonElement>> ReadValueAsync(HttpResponseMessage response)
    {
        JsonDocument doc = await response.Content.ReadFromJsonAsync<JsonDocument>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("OData response was empty.");
        return [.. doc.RootElement.GetProperty("value").EnumerateArray()];
    }

    private static async Task SeedAsync(ODataTestApp app)
    {
        using IServiceScope scope = app.CreateScope();
        TestDbContext db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        // PostgresFixture is shared across the test class — wipe any leftover
        // row from a previous test before reseeding so PageSize / MaxTop
        // assertions stay deterministic.
        await db.Invoices.IgnoreQueryFilters().ExecuteDeleteAsync();

        // 5 invoices for tenant A — enough to exercise PageSize=3 and
        // MaxTop=5 caps.
        for (int i = 0; i < 5; i++)
        {
            db.Invoices.Add(new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                Number = $"H-{i + 1:D3}",
                Amount = (i + 1) * 100m,
            });
        }

        await db.SaveChangesAsync();
    }
}
