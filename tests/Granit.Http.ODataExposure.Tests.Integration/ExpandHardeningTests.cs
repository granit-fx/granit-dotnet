using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// #3005 — recursive <c>$expand</c> hardening at the HTTP level, against
/// real PostgreSQL joins. Pins the three request-time gates (path whitelist
/// via the AST walk, <c>MaxExpansionDepth</c>, ADR-050 scalar minimization
/// on the expanded payload) plus the dotted-whitelist happy path.
/// </summary>
public sealed class ExpandHardeningTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = postgres;
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ODataTestApp _appFlatExpand = null!;
    private ODataTestApp _appNestedDepth1 = null!;
    private ODataTestApp _appNestedDepth2 = null!;

    public async ValueTask InitializeAsync()
    {
        // Three apps over the same container/schema, differing only in the
        // per-set expand policy:
        //   flat      — "Customer" only, depth 1 (the default).
        //   nested/1  — "Customer.Address" whitelisted but depth left at 1,
        //               so the EDM knows the nested navigation and the DEPTH
        //               gate (not the parser) rejects the request.
        //   nested/2  — two sets: "Invoices" allows only "Customer" while
        //               "InvoicesWide" allows "Customer.Address" (both depth
        //               2). The shared EDM therefore exposes the Address
        //               navigation, so /Invoices hits the per-set WHITELIST
        //               gate, provably not a parser artefact.
        _appFlatExpand = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            builder => builder.ExpandWhitelist("Customer"));

        _appNestedDepth1 = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            builder => builder.ExpandWhitelist("Customer.Address"));

        _appNestedDepth2 = await ODataTestApp.CreateAsync(
            _postgres.ConnectionString,
            builder => builder.ExpandWhitelist("Customer").MaxExpansionDepth(2),
            rateLimitPermitLimit: null,
            configureOptions: opts => opts
                .EntitySet<Invoice, InvoiceQueryDefinition>("InvoicesWide")
                .AllowAnonymousAccess()
                .ExpandWhitelist("Customer.Address")
                .MaxExpansionDepth(2));

        await SeedAsync(_appFlatExpand);
    }

    public async ValueTask DisposeAsync()
    {
        await _appFlatExpand.DisposeAsync();
        await _appNestedDepth1.DisposeAsync();
        await _appNestedDepth2.DisposeAsync();
    }

    [Fact]
    public async Task Expand_SingleLevel_ReturnsCustomer_WithExportScalarsOnly()
    {
        // ADR-050 acceptance: the expanded Customer carries exactly its
        // export-derived scalars (Name, Email). InternalScore is a public
        // CLR property with a real DB column — it must appear NEITHER in
        // $metadata nor in the payload. Address stays out too: the flat app
        // whitelists only "Customer".
        HttpResponseMessage response = await SendAsync(
            _appFlatExpand, "/api/granit/odata/Invoices?$expand=Customer");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement invoice = (await ReadValueAsync(response)).ShouldHaveSingleItem();
        JsonElement customer = invoice.GetProperty("Customer");
        customer.GetProperty("Name").GetString().ShouldBe("ACME");
        customer.GetProperty("Email").GetString().ShouldBe("acme@example.test");
        customer.TryGetProperty("InternalScore", out _).ShouldBeFalse();
        customer.TryGetProperty("Address", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Expand_CaseInsensitivePath_Accepted()
    {
        HttpResponseMessage response = await SendAsync(
            _appFlatExpand, "/api/granit/odata/Invoices?$expand=customer");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Expand_NestedBeyondMaxDepth_Returns400()
    {
        // Whitelist allows the dotted path, depth (default 1) does not —
        // the depth gate must fire with an actionable Problem.
        HttpResponseMessage response = await SendAsync(
            _appNestedDepth1, "/api/granit/odata/Invoices?$expand=Customer($expand=Address)");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("depth");
        body.ShouldContain("Customer.Address");
    }

    [Fact]
    public async Task Expand_NestedUnwhitelistedPath_Returns400()
    {
        // The EDM knows Customer.Address (the "InvoicesWide" sibling set
        // whitelists it), but THIS set only allows "Customer" — the AST
        // walk rejects the nested path per set.
        HttpResponseMessage response = await SendAsync(
            _appNestedDepth2, "/api/granit/odata/Invoices?$expand=Customer($expand=Address)");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Customer.Address");
        body.ShouldContain("not permitted");
    }

    [Fact]
    public async Task Expand_NestedWhitelistedWithinDepth_Returns200_WithNestedScalars()
    {
        HttpResponseMessage response = await SendAsync(
            _appNestedDepth2, "/api/granit/odata/InvoicesWide?$expand=Customer($expand=Address)");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        JsonElement invoice = (await ReadValueAsync(response)).ShouldHaveSingleItem();
        JsonElement address = invoice.GetProperty("Customer").GetProperty("Address");
        address.GetProperty("City").GetString().ShouldBe("Brussels");
        // Zip has a DB column but no export field — minimized out (ADR-050).
        address.TryGetProperty("Zip", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task Expand_TopLevelOnly_OnNestedWhitelist_Accepted()
    {
        // "Customer.Address" whitelisted implies its prefix "Customer" is
        // expandable on its own — depth 1 request against the nested/1 app.
        HttpResponseMessage response = await SendAsync(
            _appNestedDepth1, "/api/granit/odata/Invoices?$expand=Customer");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<HttpResponseMessage> SendAsync(ODataTestApp app, string url)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, url);
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());
        return await app.Client.SendAsync(request, TestContext.Current.CancellationToken);
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

        await db.Invoices.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Customers.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Addresses.ExecuteDeleteAsync();

        Address address = new() { Id = Guid.NewGuid(), City = "Brussels", Zip = "1000" };
        Customer customer = new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            Name = "ACME",
            Email = "acme@example.test",
            InternalScore = 87,
            AddressId = address.Id,
        };

        db.Addresses.Add(address);
        db.Customers.Add(customer);
        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            TenantId = TenantA,
            Number = "E-001",
            Amount = 100m,
            CustomerId = customer.Id,
        });

        await db.SaveChangesAsync();
    }
}
