using Granit.Domain;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

// Group-by on a nested EF Core complex-type member (a => a.Value.Country), stored as the dotted path
// "Value.Country" and translated to a SQL GROUP BY on the mapped column. End-to-end against real
// SQLite. The motivating case is granit-business Parties (PartyAddress.Value : Address complex type).
public sealed class NestedComplexGroupByTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<PartyCtx> _options;
    private readonly List<string> _sql = [];

    public NestedComplexGroupByTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<PartyCtx>()
            .UseSqlite(_connection)
            .LogTo(_sql.Add, [RelationalEventId.CommandExecuted])
            .Options;

        using PartyCtx seed = new(_options);
        seed.Database.EnsureCreated();
        seed.Addresses.AddRange(
            new PartyAddress { Id = Guid.NewGuid(), Kind = AddressKind.Billing, Value = new Address { Country = "BE", City = "Brussels", Region = new GeoRegion { Continent = "EU" } } },
            new PartyAddress { Id = Guid.NewGuid(), Kind = AddressKind.Shipping, Value = new Address { Country = "BE", City = "Ghent", Region = new GeoRegion { Continent = "EU" } } },
            new PartyAddress { Id = Guid.NewGuid(), Kind = AddressKind.Billing, Value = new Address { Country = "FR", City = "Paris", Region = new GeoRegion { Continent = "EU" } } },
            new PartyAddress { Id = Guid.NewGuid(), Kind = AddressKind.Billing, Value = new Address { Country = "NL", City = "Amsterdam", Region = new GeoRegion { Continent = "NA" } } });
        seed.SaveChanges();
    }

    [Fact]
    public void AllowGroupBy_on_a_nested_complex_member_does_not_throw_and_records_the_dotted_path()
    {
        QueryDefinition<PartyAddress> definition = new AddressQueryDefinition();

        Should.NotThrow(() => definition.GetGroupByFields());
        definition.GetGroupByFields().Select(g => g.PropertyName)
            .ShouldBe(["Kind", "Value.Country", "Value.Region.Continent"]);
    }

    [Fact]
    public async Task ExecuteGroupedAsync_groups_by_the_nested_member_with_correct_counts_server_side()
    {
        await using PartyCtx ctx = new(_options);
        QueryEngine<PartyAddress> engine = NewEngine();

        GroupedResult<PartyAddress> result = await engine.ExecuteGroupedAsync(
            ctx.Addresses,
            new QueryRequest { GroupBy = "Value.Country" },
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(4);
        result.Groups.Count.ShouldBe(3);
        result.Groups.First(g => (string)g.Value! == "BE").Count.ShouldBe(2);
        result.Groups.First(g => (string)g.Value! == "FR").Count.ShouldBe(1);
        result.Groups.ShouldAllBe(g => g.Field == "Value.Country");

        // The GROUP BY must execute server-side (no client-eval): EF would throw on an
        // untranslatable GroupBy, and the emitted SQL groups on the mapped complex column.
        _sql.ShouldContain(s => s.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteGroupedAsync_groups_by_a_two_level_nested_member()
    {
        // Mirrors granit-business Invoice: IssuedBillingAddressSnapshot.Address.Country — a complex
        // type nested inside another complex type. Proves the dotted path resolves at depth > 1.
        await using PartyCtx ctx = new(_options);
        QueryEngine<PartyAddress> engine = NewEngine();

        GroupedResult<PartyAddress> result = await engine.ExecuteGroupedAsync(
            ctx.Addresses,
            new QueryRequest { GroupBy = "Value.Region.Continent" },
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(4);
        result.Groups.First(g => (string)g.Value! == "EU").Count.ShouldBe(3);
        result.Groups.First(g => (string)g.Value! == "NA").Count.ShouldBe(1);
        _sql.ShouldContain(s => s.Contains("GROUP BY", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ExecuteGroupedAsync_with_projection_buckets_items_under_the_nested_key()
    {
        await using PartyCtx ctx = new(_options);
        QueryEngine<PartyAddress> engine = NewEngine();

        GroupedResult<CitySummary> result = await engine.ExecuteGroupedAsync(
            ctx.Addresses,
            new QueryRequest { GroupBy = "Value.Country" },
            a => new CitySummary(a.Value.City),
            TestContext.Current.CancellationToken);

        GroupEntry<CitySummary> be = result.Groups.First(g => (string)g.Value! == "BE");
        be.Items.ShouldNotBeNull();
        be.Items!.Select(i => i.City).ShouldBe(["Brussels", "Ghent"], ignoreOrder: true);
    }

    [Fact]
    public async Task ExecuteGroupedAsync_single_level_group_by_still_works()
    {
        await using PartyCtx ctx = new(_options);
        QueryEngine<PartyAddress> engine = NewEngine();

        GroupedResult<PartyAddress> result = await engine.ExecuteGroupedAsync(
            ctx.Addresses,
            new QueryRequest { GroupBy = "Kind" },
            TestContext.Current.CancellationToken);

        result.TotalCount.ShouldBe(4);
        result.Groups.First(g => g.Label == nameof(AddressKind.Billing)).Count.ShouldBe(3);
    }

    [Fact]
    public async Task ExecuteGroupedAsync_non_whitelisted_nested_path_returns_empty()
    {
        await using PartyCtx ctx = new(_options);
        QueryEngine<PartyAddress> engine = NewEngine();

        GroupedResult<PartyAddress> result = await engine.ExecuteGroupedAsync(
            ctx.Addresses,
            new QueryRequest { GroupBy = "Value.City" },
            TestContext.Current.CancellationToken);

        result.Groups.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public void AllowGroupBy_drilling_into_a_single_value_object_value_still_throws()
    {
        QueryDefinitionBuilder<Party> builder = new();

        ArgumentException ex = Should.Throw<ArgumentException>(
            () => builder.AllowGroupBy(p => p.Slug.Value));

        ex.Message.ShouldContain("#2767");
    }

    private static QueryEngine<PartyAddress> NewEngine() => new(
        new AddressQueryDefinition(),
        NullLogger<QueryEngine<PartyAddress>>.Instance,
        Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

    public void Dispose() => _connection.Dispose();

    private sealed class AddressQueryDefinition : QueryDefinition<PartyAddress>
    {
        public override string Name => "Test.PartyAddresses";

        protected override void Configure(QueryDefinitionBuilder<PartyAddress> builder) =>
            builder
                .Column(a => a.Kind)
                .AllowGroupBy(a => a.Kind)
                .AllowGroupBy(a => a.Value.Country)
                .AllowGroupBy(a => a.Value.Region.Continent);
    }

    private sealed record CitySummary(string City);

    // Public so the framework's dynamic ToListAsync binder can bind the enum group key.
    public enum AddressKind
    {
        Billing,
        Shipping,
    }

    private sealed class Address
    {
        public required string Country { get; init; }
        public required string City { get; init; }
        public required GeoRegion Region { get; init; }
    }

    private sealed class GeoRegion
    {
        public required string Continent { get; init; }
    }

    private sealed class PartyAddress
    {
        public Guid Id { get; set; }
        public AddressKind Kind { get; set; }
        public required Address Value { get; init; }
    }

    private sealed class Slug : SingleValueObject<string>
    {
        public override required string Value { get; init; }
        public static Slug Create(string v) => new() { Value = v };
    }

    // Separate entity used only to prove the #2767 SingleValueObject `.Value` drill guard still fires.
    private sealed class Party
    {
        public Guid Id { get; set; }
        public Slug Slug { get; set; } = null!;
    }

    private sealed class PartyCtx(DbContextOptions<PartyCtx> options) : DbContext(options)
    {
        public DbSet<PartyAddress> Addresses => Set<PartyAddress>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<PartyAddress>(e =>
            {
                e.HasKey(p => p.Id);
                e.ComplexProperty(p => p.Value, v => v.ComplexProperty(a => a.Region));
            });
    }
}
