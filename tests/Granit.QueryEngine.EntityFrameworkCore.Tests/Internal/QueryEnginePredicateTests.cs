using System.Linq.Expressions;
using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Filtering.Exceptions;
using Granit.QueryEngine.Meta;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class QueryEnginePredicateTests : IAsyncLifetime
{
    private TestDbContext _db = null!;

    private sealed class ProductQueryDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.PredicateProducts";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Filterable())
                .Column(p => p.Price, c => c.Filterable())
                .Column(p => p.Category, c => c.Filterable())
                .Column(p => p.CreatedAt, c => c.Filterable());
    }

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new TestDbContext(options);

        _db.Products.AddRange(
            new TestProduct { Id = Guid.NewGuid(), Name = "Laptop", Price = 1000, Category = ProductCategory.Electronics, CreatedAt = DateTimeOffset.UtcNow },
            new TestProduct { Id = Guid.NewGuid(), Name = "Novel", Price = 15, Category = ProductCategory.Books, CreatedAt = null },
            new TestProduct { Id = Guid.NewGuid(), Name = "T-Shirt", Price = 25, Category = ProductCategory.Clothing, CreatedAt = null },
            new TestProduct { Id = Guid.NewGuid(), Name = "Phone", Price = 800, Category = ProductCategory.Electronics, CreatedAt = DateTimeOffset.UtcNow });

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    private static QueryEngine<TestProduct> CreateEngine() =>
        new(
            new ProductQueryDefinition(),
            NullLogger<QueryEngine<TestProduct>>.Instance,
            Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

    [Fact]
    public void BuildFilteredQuery_with_predicate_filters_rows()
    {
        QueryEngine<TestProduct> engine = CreateEngine();
        var predicate = QueryPredicate.Or(
            QueryPredicate.Criterion("Price", FilterOperator.Gt, "900"),
            QueryPredicate.Criterion("CreatedAt", FilterOperator.IsNull));

        var result = engine
            .BuildFilteredQuery(_db.Products.AsQueryable(), new QueryRequest(), predicate)
            .ToList();

        result.Select(p => p.Name).ShouldBe(["Laptop", "Novel", "T-Shirt"], ignoreOrder: true);
    }

    [Fact]
    public void Predicate_is_AND_combined_with_the_lenient_request_filter()
    {
        QueryEngine<TestProduct> engine = CreateEngine();
        QueryRequest request = new()
        {
            Filter = new Dictionary<string, string> { ["Category.eq"] = "Electronics" },
        };
        var predicate = QueryPredicate.Not(
            QueryPredicate.Criterion("Name", FilterOperator.Eq, "Phone"));

        var result = engine
            .BuildFilteredQuery(_db.Products.AsQueryable(), request, predicate)
            .ToList();

        result.ShouldHaveSingleItem().Name.ShouldBe("Laptop");
    }

    [Fact]
    public void Null_predicate_is_equivalent_to_the_two_argument_overload()
    {
        QueryEngine<TestProduct> engine = CreateEngine();

        var result = engine
            .BuildFilteredQuery(_db.Products.AsQueryable(), new QueryRequest(), predicate: null)
            .ToList();

        result.Count.ShouldBe(4);
    }

    [Fact]
    public void Invalid_predicate_throws_instead_of_returning_a_superset()
    {
        QueryEngine<TestProduct> engine = CreateEngine();
        var predicate = QueryPredicate.Or(
            QueryPredicate.Criterion("Ghost", FilterOperator.Eq, "x"),
            QueryPredicate.Criterion("Name", FilterOperator.Eq, "Laptop"));

        QueryPredicateValidationException exception = Should.Throw<QueryPredicateValidationException>(
            () => engine.BuildFilteredQuery(_db.Products.AsQueryable(), new QueryRequest(), predicate));

        exception.Errors.ShouldHaveSingleItem().Code.ShouldBe(QueryPredicateErrorCodes.UnknownField);
    }

    [Fact]
    public void GetMetadata_advertises_null_checks_only_on_nullable_columns()
    {
        QueryEngine<TestProduct> engine = CreateEngine();

        QueryMetadata metadata = engine.GetMetadata();

        FilterableField createdAt = metadata.FilterableFields.Single(f => f.Name == "CreatedAt");
        createdAt.Operators.ShouldContain(FilterOperator.IsNull);
        createdAt.Operators.ShouldContain(FilterOperator.IsNotNull);

        FilterableField price = metadata.FilterableFields.Single(f => f.Name == "Price");
        price.Operators.ShouldContain(FilterOperator.Ne);
        price.Operators.ShouldNotContain(FilterOperator.IsNull);
    }

    [Fact]
    public void Default_interface_implementation_throws_NotSupported_for_a_predicate()
    {
        IQueryEngine<TestProduct> legacy = new LegacyQueryEngine();
        var predicate = QueryPredicate.Criterion("Name", FilterOperator.Eq, "x");

        Should.Throw<NotSupportedException>(
            () => legacy.BuildFilteredQuery(Array.Empty<TestProduct>().AsQueryable(), new QueryRequest(), predicate));
    }

    [Fact]
    public void Default_interface_implementation_delegates_when_predicate_is_null()
    {
        IQueryEngine<TestProduct> legacy = new LegacyQueryEngine();
        IQueryable<TestProduct> source = Array.Empty<TestProduct>().AsQueryable();

        IQueryable<TestProduct> result = legacy.BuildFilteredQuery(source, new QueryRequest(), predicate: null);

        result.ShouldBeSameAs(source);
    }

    /// <summary>
    /// Minimal out-of-repo-style implementor that predates the predicate overload: it only
    /// implements the abstract members, proving the default interface implementation keeps
    /// such implementations compiling (pre-1.0 courtesy).
    /// </summary>
    private sealed class LegacyQueryEngine : IQueryEngine<TestProduct>
    {
        public Task<PagedResult<TestProduct>> ExecuteAsync(
            IQueryable<TestProduct> source, QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<PagedResult<TProjection>> ExecuteAsync<TProjection>(
            IQueryable<TestProduct> source, QueryRequest request,
            Expression<Func<TestProduct, TProjection>> projection, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GroupedResult<TestProduct>> ExecuteGroupedAsync(
            IQueryable<TestProduct> source, QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<GroupedResult<TProjection>> ExecuteGroupedAsync<TProjection>(
            IQueryable<TestProduct> source, QueryRequest request,
            Expression<Func<TestProduct, TProjection>> projection, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public IAsyncEnumerable<TestProduct> ExecuteStreamAsync(
            IQueryable<TestProduct> source, QueryRequest request, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public QueryMetadata GetMetadata() => throw new NotImplementedException();

        public IQueryable<TestProduct> BuildFilteredQuery(IQueryable<TestProduct> source, QueryRequest request) =>
            source;
    }
}
