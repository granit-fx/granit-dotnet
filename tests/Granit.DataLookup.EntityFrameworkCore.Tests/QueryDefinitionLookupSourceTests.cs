using Granit.DataLookup.Descriptors;
using Granit.DataLookup.EntityFrameworkCore.Sources;
using Granit.DataLookup.Registry;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.EntityFrameworkCore.Tests;

public sealed class QueryDefinitionLookupSourceTests : IDisposable
{
    private readonly SampleDbContext _db;
    private readonly Guid _acmeId = Guid.NewGuid();

    public QueryDefinitionLookupSourceTests()
    {
        DbContextOptions<SampleDbContext> options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        _db = new SampleDbContext(options);

        _db.Tenants.AddRange(
            new Tenant(_acmeId, "Acme", "EU"),
            new Tenant(Guid.NewGuid(), "Initech", "US"));
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Ctor_throws_when_definition_declares_no_lookup()
    {
        IQueryEngine<Tenant> engine = Substitute.For<IQueryEngine<Tenant>>();

        Should.Throw<InvalidOperationException>(() =>
            new QueryDefinitionLookupSource<Tenant>(engine, new NoLookupDefinition(), () => _db.Tenants));
    }

    [Fact]
    public void Exposes_name_permission_scope_and_kind()
    {
        QueryDefinitionLookupSource<Tenant> source = BuildSource(out _);

        source.Name.ShouldBe("tenants");
        source.RequiredPermission.ShouldBe("Platform.Tenants.Read");
        source.ScopeKeys.ShouldBe(["region"]);
        ((IKindProviderLookupSource)source).Kind.ShouldBe(LookupKind.QueryEngine);
    }

    [Fact]
    public async Task Search_maps_query_and_projects_items()
    {
        QueryDefinitionLookupSource<Tenant> source = BuildSource(out IQueryEngine<Tenant> engine);
        QueryRequest? captured = null;
        engine.ExecuteAsync(Arg.Any<IQueryable<Tenant>>(), Arg.Do<QueryRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<Tenant>([new Tenant(_acmeId, "Acme", "EU")], TotalCount: 1, HasMore: false)));

        LookupResult result = await source.SearchAsync(
            new LookupQuery(Search: "ac", Page: 2, PageSize: 10),
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Search.ShouldBe("ac");
        captured.Page.ShouldBe(2);
        captured.PageSize.ShouldBe(10);
        captured.Cursor.ShouldBeNull();
        result.Items.Single().Value.ShouldBe(_acmeId);
        result.Items.Single().Label.ShouldBe("Acme");
        result.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Search_passes_continuation_token_as_cursor_and_returns_next_cursor()
    {
        QueryDefinitionLookupSource<Tenant> source = BuildSource(out IQueryEngine<Tenant> engine);
        QueryRequest? captured = null;
        engine.ExecuteAsync(Arg.Any<IQueryable<Tenant>>(), Arg.Do<QueryRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<Tenant>([], TotalCount: null, HasMore: true, NextCursor: "next-cursor")));

        LookupResult result = await source.SearchAsync(
            new LookupQuery(Page: 5, ContinuationToken: "cur-1"),
            TestContext.Current.CancellationToken);

        captured!.Cursor.ShouldBe("cur-1");
        captured.Page.ShouldBeNull();
        result.ContinuationToken.ShouldBe("next-cursor");
    }

    [Fact]
    public async Task Search_maps_scope_to_equality_filter_only_for_filterable_columns()
    {
        QueryDefinitionLookupSource<Tenant> source = BuildSource(out IQueryEngine<Tenant> engine);
        QueryRequest? captured = null;
        engine.ExecuteAsync(Arg.Any<IQueryable<Tenant>>(), Arg.Do<QueryRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PagedResult<Tenant>([], TotalCount: 0, HasMore: false)));

        Dictionary<string, string?> scope = new() { ["region"] = "EU", ["unknown"] = "x", ["blank"] = "  " };
        await source.SearchAsync(new LookupQuery(Scope: scope), TestContext.Current.CancellationToken);

        captured!.Filter.ShouldNotBeNull();
        captured.Filter!.Count.ShouldBe(1);
        captured.Filter["Region.eq"].ShouldBe("EU");
        captured.Filter.ContainsKey("unknown.eq").ShouldBeFalse();
    }

    [Fact]
    public async Task Resolve_returns_item_by_value()
    {
        QueryDefinitionLookupSource<Tenant> source = BuildSource(out _);

        LookupItem? item = await source.ResolveByValueAsync(_acmeId, TestContext.Current.CancellationToken);

        item.ShouldNotBeNull();
        item!.Value.ShouldBe(_acmeId);
        item.Label.ShouldBe("Acme");
    }

    [Fact]
    public async Task Resolve_accepts_string_form_of_value()
    {
        QueryDefinitionLookupSource<Tenant> source = BuildSource(out _);

        LookupItem? item = await source.ResolveByValueAsync(_acmeId.ToString(), TestContext.Current.CancellationToken);

        item.ShouldNotBeNull();
        item!.Label.ShouldBe("Acme");
    }

    [Fact]
    public async Task Resolve_returns_null_for_unknown_value()
    {
        QueryDefinitionLookupSource<Tenant> source = BuildSource(out _);

        LookupItem? item = await source.ResolveByValueAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        item.ShouldBeNull();
    }

    private QueryDefinitionLookupSource<Tenant> BuildSource(out IQueryEngine<Tenant> engine)
    {
        engine = Substitute.For<IQueryEngine<Tenant>>();
        return new QueryDefinitionLookupSource<Tenant>(engine, new TenantLookupDefinition(), () => _db.Tenants);
    }

    public sealed record Tenant(Guid Id, string Name, string Region);

    private sealed class TenantLookupDefinition : QueryDefinition<Tenant>
    {
        public override string Name => "Test.Tenants";

        protected override void Configure(QueryDefinitionBuilder<Tenant> builder) =>
            builder
                .Column(t => t.Name, c => c.Sortable().Filterable())
                .Column(t => t.Region, c => c.Filterable())
                .GlobalSearch(t => t.Name!)
                .DefaultSort("Name")
                .SupportsCursorPagination(t => t.Id)
                .AsLookup("tenants", t => t.Id, t => t.Name,
                    requiredPermission: "Platform.Tenants.Read",
                    scopeKeys: ["region"]);
    }

    private sealed class NoLookupDefinition : QueryDefinition<Tenant>
    {
        public override string Name => "Test.NoLookup";

        protected override void Configure(QueryDefinitionBuilder<Tenant> builder) =>
            builder.Column(t => t.Name);
    }

    internal sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();

        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<Tenant>().HasKey(t => t.Id);
    }
}
