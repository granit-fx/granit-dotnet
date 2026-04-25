using Granit.DataLookup.Descriptors;
using Granit.DataLookup.EntityFrameworkCore.Sources;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace Granit.DataLookup.EntityFrameworkCore.Tests;

public sealed class QueryableLookupSourceTests : IDisposable
{
    private readonly SampleDbContext _db;

    public QueryableLookupSourceTests()
    {
        DbContextOptions<SampleDbContext> options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseInMemoryDatabase($"test-{Guid.NewGuid()}")
            .Options;
        _db = new SampleDbContext(options);

        _db.Tenants.AddRange(
            new Tenant(Guid.NewGuid(), "Acme"),
            new Tenant(Guid.NewGuid(), "Initech"),
            new Tenant(Guid.NewGuid(), "Wonka"));
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Search_returns_all_items_sorted_by_label()
    {
        QueryableLookupSource<Tenant> source = BuildSource();

        LookupResult result = await source.SearchAsync(new LookupQuery(), TestContext.Current.CancellationToken);

        result.Items.Select(i => i.Label).ShouldBe(["Acme", "Initech", "Wonka"]);
        result.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task Search_respects_search_predicate()
    {
        QueryableLookupSource<Tenant> source = BuildSource();

        LookupResult result = await source.SearchAsync(
            new LookupQuery(Search: "ni"),
            TestContext.Current.CancellationToken);

        result.Items.Select(i => i.Label).ShouldBe(["Initech"]);
    }

    [Fact]
    public async Task Resolve_returns_item_by_id()
    {
        Tenant tenant = _db.Tenants.First(t => t.Name == "Acme");
        QueryableLookupSource<Tenant> source = BuildSource();

        LookupItem? item = await source.ResolveByValueAsync(tenant.Id, TestContext.Current.CancellationToken);

        item.ShouldNotBeNull();
        item!.Value.ShouldBe(tenant.Id);
        item.Label.ShouldBe("Acme");
    }

    [Fact]
    public async Task Resolve_returns_null_for_unknown_id()
    {
        QueryableLookupSource<Tenant> source = BuildSource();

        LookupItem? item = await source.ResolveByValueAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        item.ShouldBeNull();
    }

    [Fact]
    public void Exposes_scope_keys_and_required_permission()
    {
        QueryableLookupSource<Tenant> source = new(
            name: "tenants",
            queryableFactory: () => _db.Tenants.AsQueryable(),
            valueSelector: t => t.Id,
            labelSelector: t => t.Name,
            requiredPermission: "MultiTenancy.Tenants.Read",
            scopeKeys: ["orgId"]);

        source.Name.ShouldBe("tenants");
        source.RequiredPermission.ShouldBe("MultiTenancy.Tenants.Read");
        source.ScopeKeys.ShouldBe(["orgId"]);
    }

    private QueryableLookupSource<Tenant> BuildSource() =>
        new(
            name: "tenants",
            queryableFactory: () => _db.Tenants.AsQueryable(),
            valueSelector: t => t.Id,
            labelSelector: t => t.Name,
            searchPredicate: (t, search) => t.Name.Contains(search));

    public sealed record Tenant(Guid Id, string Name);

    internal sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
    {
        public DbSet<Tenant> Tenants => Set<Tenant>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Tenant>().HasKey(t => t.Id);
        }
    }
}
