using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// #3004 end-to-end assertions for the <c>$filter</c> → QueryPredicate translation layer:
/// the filter shapes Power BI emits fold into SQL through the engine's single enforcement
/// point, untranslatable or non-whitelisted constructs return <c>400</c> with an actionable
/// Problem detail, and the SQL capture proves the translated filter reaches the database
/// exactly once (never re-applied by <c>ApplyTo</c>).
/// </summary>
public sealed class ODataFilterTranslationTests(PostgresFixture postgres)
    : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres = postgres;
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private ODataTestApp _app = null!;

    public async ValueTask InitializeAsync()
    {
        _app = await ODataTestApp.CreateAsync(_postgres.ConnectionString);
        await SeedAsync();
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // ── Happy shapes (what Power BI query folding emits) ─────────────────

    [Theory]
    [InlineData("Status eq 'Paid' and Amount gt 100", new[] { "A-002" })]
    [InlineData("Status eq 'Draft' or Amount gt 400", new[] { "A-004", "A-005" })]
    [InlineData("Status eq 'Paid' or Status eq 'Sent' or Amount gt 400", new[] { "A-001", "A-002", "A-003", "A-005" })]
    [InlineData("Status in ('Paid','Sent')", new[] { "A-001", "A-002", "A-003", "A-005" })]
    [InlineData("contains(Number, '003')", new[] { "A-003" })]
    [InlineData("startswith(Number, 'A-')", new[] { "A-001", "A-002", "A-003", "A-004", "A-005" })]
    [InlineData("endswith(Number, '1')", new[] { "A-001" })]
    [InlineData("not contains(Number, '003')", new[] { "A-001", "A-002", "A-004", "A-005" })]
    [InlineData("PaidAt ne null", new[] { "A-001", "A-002", "A-005" })]
    [InlineData("PaidAt eq null", new[] { "A-003", "A-004" })]
    [InlineData(
        "PaidAt ge 2026-01-01T00:00:00Z and PaidAt lt 2026-02-01T00:00:00Z",
        new[] { "A-001", "A-005" })]
    public async Task Filter_SupportedShape_ReturnsExpectedRows(string filter, string[] expectedNumbers)
    {
        HttpResponseMessage response = await GetAsync($"Invoices?$filter={Uri.EscapeDataString(filter)}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.Select(r => r.GetProperty("Number").GetString())
            .Order()
            .ShouldBe(expectedNumbers.Order(), $"filter: {filter}");
    }

    // ── Rejections → 400 Problem ─────────────────────────────────────────

    [Theory]
    // BREAKING (#3004): EDM-visible but non-Filterable() column — used to pass through
    // ApplyTo silently, now rejected by the engine's strict predicate validation with the
    // field name in the detail.
    [InlineData("InternalNote eq 'x'", "InternalNote")]
    [InlineData("InternalNote eq 'x'", "FieldNotFilterable")]
    // unsupported canonical functions
    [InlineData("tolower(Status) eq 'paid'", "tolower")]
    [InlineData("year(PaidAt) eq 2026", "year")]
    // arithmetic
    [InlineData("Amount add 5 gt 10", "Arithmetic")]
    // in-list item containing the engine's separator
    [InlineData("Status in ('a,b')", "comma")]
    public async Task Filter_RejectedShape_Returns400WithActionableDetail(string filter, string expectedFragment)
    {
        HttpResponseMessage response = await GetAsync($"Invoices?$filter={Uri.EscapeDataString(filter)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain(expectedFragment, customMessage: body);
        body.ShouldContain("Query option not supported");
    }

    [Fact]
    public async Task Filter_NavigationAccess_Returns400()
    {
        // The test EDM exposes no Customer navigation, so the parser rejects the path —
        // surfaced as a 400 Problem instead of an unhandled ODataException (pre-#3004).
        HttpResponseMessage response = await GetAsync(
            $"Invoices?$filter={Uri.EscapeDataString("Customer/Name eq 'ACME'")}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Customer", customMessage: body);
    }

    [Fact]
    public async Task OrderBy_NonSortableColumn_Returns400()
    {
        // Status is Filterable but NOT Sortable in the QueryDefinition — the per-route
        // AllowedOrderByProperties whitelist (derived from SortableFields) rejects it.
        HttpResponseMessage response = await GetAsync("Invoices?$orderby=Status");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Status", customMessage: body);
    }

    [Fact]
    public async Task OrderBy_SortableColumn_ReturnsOrderedRows()
    {
        HttpResponseMessage response = await GetAsync("Invoices?$orderby=Amount desc");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IReadOnlyList<JsonElement> rows = await ReadValueAsync(response);
        rows.Count.ShouldBe(5);
        rows[0].GetProperty("Number").GetString().ShouldBe("A-005");
    }

    // ── Single-application proof (SQL capture) ───────────────────────────

    [Fact]
    public async Task Filter_TranslatedPredicate_ReachesSqlExactlyOnce()
    {
        // With the pre-#3004 flow, ApplyTo would compose the $filter a second time on top of
        // the engine's predicate. The ignore flag on ApplyTo guarantees each filtered column
        // appears exactly once in the WHERE clause.
        _app.SqlCapture.Clear();
        HttpResponseMessage response = await GetAsync(
            $"Invoices?$filter={Uri.EscapeDataString("Status eq 'Paid' and Amount gt 100")}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string? selectCommand = _app.SqlCapture.Commands
            .FirstOrDefault(c => c.Contains(@"FROM ""Invoices""", StringComparison.Ordinal));
        selectCommand.ShouldNotBeNull("expected a SELECT against \"Invoices\" in the captured SQL");

        int whereIndex = selectCommand.IndexOf("WHERE", StringComparison.Ordinal);
        whereIndex.ShouldBeGreaterThan(0, "expected a WHERE clause in the captured SQL");
        string whereClause = selectCommand[whereIndex..];

        CountOccurrences(whereClause, @"""Status""").ShouldBe(1,
            $"user $filter column must appear exactly once in the WHERE clause — actual SQL:\n{selectCommand}");
        CountOccurrences(whereClause, @"""Amount""").ShouldBe(1,
            $"user $filter column must appear exactly once in the WHERE clause — actual SQL:\n{selectCommand}");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        for (int index = haystack.IndexOf(needle, StringComparison.Ordinal);
             index >= 0;
             index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private async Task<HttpResponseMessage> GetAsync(string relativePath)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"/api/granit/odata/{relativePath}");
        request.Headers.Add("X-Test-Tenant", TenantA.ToString());
        return await _app.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<JsonElement>> ReadValueAsync(HttpResponseMessage response)
    {
        JsonDocument doc = await response.Content.ReadFromJsonAsync<JsonDocument>(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("OData response was empty.");
        return [.. doc.RootElement.GetProperty("value").EnumerateArray()];
    }

    private async Task SeedAsync()
    {
        using IServiceScope scope = _app.CreateScope();
        TestDbContext db = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await db.Invoices.IgnoreQueryFilters().ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        db.Invoices.AddRange(
            new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                Number = "A-001",
                Amount = 100m,
                Status = "Paid",
                PaidAt = new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero),
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                Number = "A-002",
                Amount = 250m,
                Status = "Paid",
                PaidAt = new DateTimeOffset(2026, 2, 10, 0, 0, 0, TimeSpan.Zero),
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                Number = "A-003",
                Amount = 250m,
                Status = "Sent",
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                Number = "A-004",
                Amount = 400m,
                Status = "Draft",
                InternalNote = "escalated",
            },
            new Invoice
            {
                Id = Guid.NewGuid(),
                TenantId = TenantA,
                Number = "A-005",
                Amount = 500m,
                Status = "Sent",
                PaidAt = new DateTimeOffset(2026, 1, 20, 0, 0, 0, TimeSpan.Zero),
            });

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
