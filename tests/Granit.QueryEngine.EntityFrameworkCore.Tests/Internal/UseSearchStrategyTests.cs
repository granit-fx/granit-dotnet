using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Granit.QueryEngine.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.EntityFrameworkCore.Tests.Internal;

public sealed class UseSearchStrategyTests : IAsyncLifetime
{
    private TestDbContext _db = null!;

    /// <summary>
    /// Marker strategy: ignores the search properties entirely and matches the
    /// fixed name "Laptop" so tests can prove which strategy ran.
    /// </summary>
    private sealed class LaptopOnlySearchStrategy : IGlobalSearchStrategy<TestProduct>
    {
        public int Invocations { get; private set; }

        public IQueryable<TestProduct> ApplySearch(
            IQueryable<TestProduct> source,
            string searchTerm,
            IReadOnlyList<string> searchProperties)
        {
            Invocations++;
            return source.Where(p => p.Name == "Laptop");
        }
    }

    private sealed class CustomStrategyDefinition : QueryDefinition<TestProduct>
    {
        public override string Name => "Test.CustomSearchProducts";

        protected override void Configure(QueryDefinitionBuilder<TestProduct> builder) =>
            builder
                .Column(p => p.Name, c => c.Label("Name"))
                .GlobalSearch(p => p.Name)
                .UseSearchStrategy<LaptopOnlySearchStrategy>();
    }

    public async ValueTask InitializeAsync()
    {
        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _db = new TestDbContext(options);

        _db.Products.AddRange(
            new TestProduct { Id = Guid.NewGuid(), Name = "Laptop", Price = 1000, Category = ProductCategory.Electronics },
            new TestProduct { Id = Guid.NewGuid(), Name = "Phone", Price = 800, Category = ProductCategory.Electronics });

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        _db.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Declared_strategy_is_resolved_from_DI_when_registered()
    {
        LaptopOnlySearchStrategy registered = new();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(registered)
            .BuildServiceProvider();

        QueryEngine<TestProduct> engine = new(
            new CustomStrategyDefinition(),
            NullLogger<QueryEngine<TestProduct>>.Instance,
            Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()),
            serviceProvider: provider);

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { Search = "anything" },
            TestContext.Current.CancellationToken);

        registered.Invocations.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Name.ShouldBe("Laptop");
    }

    [Fact]
    public async Task Declared_strategy_is_activated_when_not_registered()
    {
        QueryEngine<TestProduct> engine = new(
            new CustomStrategyDefinition(),
            NullLogger<QueryEngine<TestProduct>>.Instance,
            Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()));

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { Search = "anything" },
            TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem().Name.ShouldBe("Laptop");
    }

    [Fact]
    public async Task Declared_strategy_wins_over_the_injected_default()
    {
        LaptopOnlySearchStrategy declared = new();
        ServiceProvider provider = new ServiceCollection()
            .AddSingleton(declared)
            .BuildServiceProvider();

        QueryEngine<TestProduct> engine = new(
            new CustomStrategyDefinition(),
            NullLogger<QueryEngine<TestProduct>>.Instance,
            Microsoft.Extensions.Options.Options.Create(new Granit.QueryEngine.Options.QueryEngineOptions()),
            searchStrategy: new ContainsSearchStrategy<TestProduct>(),
            serviceProvider: provider);

        PagedResult<TestProduct> result = await engine.ExecuteAsync(
            _db.Products.AsQueryable(),
            new QueryRequest { Search = "Phone" },
            TestContext.Current.CancellationToken);

        // ContainsSearchStrategy would have matched "Phone"; the declared strategy matched Laptop.
        declared.Invocations.ShouldBe(1);
        result.Items.ShouldHaveSingleItem().Name.ShouldBe("Laptop");
    }
}
