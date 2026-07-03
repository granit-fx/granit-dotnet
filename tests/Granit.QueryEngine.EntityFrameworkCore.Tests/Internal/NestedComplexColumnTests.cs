using System.Linq.Expressions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

// Columns declared on a nested EF Core complex-type member (a => a.Value.Street1), recorded as the
// dotted path "Value.Street1" and translated to SQL WHERE/ORDER BY on the mapped scalar column.
// End-to-end against real SQLite. Motivating case: granit-business Parties (PartyAddress.Value : Address).
public sealed class NestedComplexColumnTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AddressCtx> _options;
    private readonly List<string> _sql = [];

    public NestedComplexColumnTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AddressCtx>()
            .UseSqlite(_connection)
            .LogTo(_sql.Add, [RelationalEventId.CommandExecuted])
            .Options;

        using AddressCtx seed = new(_options);
        seed.Database.EnsureCreated();
        seed.Addresses.AddRange(
            new PartyAddress { Id = Guid.NewGuid(), Value = new Address { Street1 = "10 Rue A", City = "Brussels", Region = new GeoRegion { Continent = "EU" } } },
            new PartyAddress { Id = Guid.NewGuid(), Value = new Address { Street1 = "20 Rue B", City = "Ghent", Region = new GeoRegion { Continent = "EU" } } },
            new PartyAddress { Id = Guid.NewGuid(), Value = new Address { Street1 = "30 Rue C", City = "Paris", Region = new GeoRegion { Continent = "EU" } } },
            new PartyAddress { Id = Guid.NewGuid(), Value = new Address { Street1 = "40 Ave D", City = "New York", Region = new GeoRegion { Continent = "NA" } } });
        seed.SaveChanges();
    }

    [Fact]
    public void Column_on_a_nested_complex_member_records_the_dotted_path()
    {
        AddressQueryDefinition definition = new();

        definition.GetColumns().Select(c => c.PropertyName)
            .ShouldBe(["Value.Street1", "Value.City", "Value.Region.Continent"]);
    }

    [Fact]
    public async Task Filter_eq_on_a_nested_member_matches_server_side()
    {
        await using AddressCtx ctx = new(_options);

        PagedResult<PartyAddress> result = await NewEngine().ExecuteAsync(
            ctx.Addresses,
            new QueryRequest { Filter = new Dictionary<string, string> { ["Value.Street1.eq"] = "20 Rue B" } },
            TestContext.Current.CancellationToken);

        result.Items.Select(a => a.Value.City).ShouldBe(["Ghent"]);
        _sql.ShouldContain(s => s.Contains("WHERE", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Filter_contains_on_a_nested_member_matches_server_side()
    {
        await using AddressCtx ctx = new(_options);

        PagedResult<PartyAddress> result = await NewEngine().ExecuteAsync(
            ctx.Addresses,
            new QueryRequest { Filter = new Dictionary<string, string> { ["Value.City.contains"] = "e" } },
            TestContext.Current.CancellationToken);

        // Brussels, Ghent, New York contain 'e' (case-sensitive LIKE on SQLite collation-dependent —
        // seed values chosen so the ASCII 'e' is unambiguous).
        result.Items.Select(a => a.Value.City).OrderBy(c => c)
            .ShouldBe(["Brussels", "Ghent", "New York"]);
    }

    [Fact]
    public async Task Filter_on_a_two_level_nested_member_matches_server_side()
    {
        await using AddressCtx ctx = new(_options);

        PagedResult<PartyAddress> result = await NewEngine().ExecuteAsync(
            ctx.Addresses,
            new QueryRequest { Filter = new Dictionary<string, string> { ["Value.Region.Continent.eq"] = "NA" } },
            TestContext.Current.CancellationToken);

        result.Items.Select(a => a.Value.City).ShouldBe(["New York"]);
    }

    [Fact]
    public async Task Sort_by_a_nested_member_orders_server_side()
    {
        await using AddressCtx ctx = new(_options);

        PagedResult<PartyAddress> asc = await NewEngine().ExecuteAsync(
            ctx.Addresses,
            new QueryRequest { Sort = "Value.Street1" },
            TestContext.Current.CancellationToken);

        asc.Items.Select(a => a.Value.Street1)
            .ShouldBe(["10 Rue A", "20 Rue B", "30 Rue C", "40 Ave D"]);

        PagedResult<PartyAddress> desc = await NewEngine().ExecuteAsync(
            ctx.Addresses,
            new QueryRequest { Sort = "-Value.Street1" },
            TestContext.Current.CancellationToken);

        desc.Items.Select(a => a.Value.Street1)
            .ShouldBe(["40 Ave D", "30 Rue C", "20 Rue B", "10 Rue A"]);

        _sql.ShouldContain(s => s.Contains("ORDER BY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Filter_on_a_non_declared_nested_path_is_dropped()
    {
        await using AddressCtx ctx = new(_options);

        // PostalCode is a real column but not declared filterable — the criterion is ignored,
        // so every row is returned (matching the whitelist-first "unknown field dropped" behavior).
        PagedResult<PartyAddress> result = await NewEngine().ExecuteAsync(
            ctx.Addresses,
            new QueryRequest { Filter = new Dictionary<string, string> { ["Value.PostalCode.eq"] = "0000" } },
            TestContext.Current.CancellationToken);

        result.Items.Count.ShouldBe(4);
    }

    [Fact]
    public void CompositeCursor_resolves_and_roundtrips_a_nested_sort_field()
    {
        // A nested sort field must participate in the keyset — otherwise it is silently dropped and
        // the cursor predicate disagrees with the ORDER BY, skipping/duplicating rows at page edges.
        List<CompositeCursorBuilder.SortField> fields = CompositeCursorBuilder.ParseSortFields<PartyAddress>(
            "Value.Number",
            new HashSet<string>(["Value.Number"], StringComparer.OrdinalIgnoreCase));

        fields.Count.ShouldBe(1);
        fields[0].Path.ShouldBe("Value.Number");

        PartyAddress last = new() { Id = Guid.NewGuid(), Value = new Address { Street1 = "x", City = "Ghent", Number = 20, Region = new GeoRegion { Continent = "EU" } } };
        string cursor = CompositeCursorBuilder.EncodeCompositeCursor(last, fields);
        Dictionary<string, string>? decoded = CursorEncoder.DecodeComposite(cursor);
        decoded.ShouldNotBeNull();

        Expression<Func<PartyAddress, bool>>? predicate = CompositeCursorBuilder.BuildCursorPredicate<PartyAddress>(fields, decoded);
        predicate.ShouldNotBeNull();

        // The keyset predicate walks the nested member: rows with Value.Number > 20.
        Func<PartyAddress, bool> compiled = predicate.Compile();
        compiled(new PartyAddress { Id = Guid.NewGuid(), Value = new Address { Street1 = "x", City = "x", Number = 30, Region = new GeoRegion { Continent = "EU" } } }).ShouldBeTrue();
        compiled(new PartyAddress { Id = Guid.NewGuid(), Value = new Address { Street1 = "x", City = "x", Number = 10, Region = new GeoRegion { Continent = "EU" } } }).ShouldBeFalse();
    }

    private static QueryEngine<PartyAddress> NewEngine() => new(
        new AddressQueryDefinition(),
        NullLogger<QueryEngine<PartyAddress>>.Instance,
        Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

    public void Dispose() => _connection.Dispose();

    private sealed class AddressQueryDefinition : QueryDefinition<PartyAddress>
    {
        public override string Name => "Test.NestedColumns";

        protected override void Configure(QueryDefinitionBuilder<PartyAddress> builder) =>
            builder
                .Column(a => a.Value.Street1, c => c.Filterable().Sortable())
                .Column(a => a.Value.City, c => c.Filterable().Sortable())
                .Column(a => a.Value.Region.Continent, c => c.Filterable().Sortable());
    }

    private sealed class Address
    {
        public required string Street1 { get; init; }
        public required string City { get; init; }
        public string PostalCode { get; init; } = "";
        public int Number { get; init; }
        public required GeoRegion Region { get; init; }
    }

    private sealed class GeoRegion
    {
        public required string Continent { get; init; }
    }

    private sealed class PartyAddress
    {
        public Guid Id { get; set; }
        public required Address Value { get; init; }
    }

    private sealed class AddressCtx(DbContextOptions<AddressCtx> options) : DbContext(options)
    {
        public DbSet<PartyAddress> Addresses => Set<PartyAddress>();

        protected override void OnModelCreating(ModelBuilder b) =>
            b.Entity<PartyAddress>(e =>
            {
                e.HasKey(p => p.Id);
                e.ComplexProperty(p => p.Value, v => v.ComplexProperty(a => a.Region));
            });
    }
}
