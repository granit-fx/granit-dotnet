using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Options;
using Granit.QueryEngine.Search;
using Shouldly;
using Xunit;

namespace Granit.QueryEngine.Tests;

public sealed class QueryDefinitionBuilderAdditionalTests
{
    [Fact]
    public void Constructor_with_options_uses_custom_defaults()
    {
        QueryEngineOptions options = new()
        {
            DefaultPageSize = 50,
            MaxPageSize = 500,
            MaxStreamSize = 200_000,
        };

        QueryDefinitionBuilder<TestEntity> builder = new(options);

        builder.DefaultPageSizeValue.ShouldBe(50);
        builder.MaxPageSizeValue.ShouldBe(500);
        builder.MaxStreamSizeValue.ShouldBe(200_000);
    }

    [Fact]
    public void MaxStreamSize_sets_value()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.MaxStreamSize(50_000);

        builder.MaxStreamSizeValue.ShouldBe(50_000);
    }

    [Fact]
    public void MaxStreamSize_default_is_100000()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.MaxStreamSizeValue.ShouldBe(100_000);
    }

    [Fact]
    public void MaxStreamSize_throws_for_zero()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            builder.MaxStreamSize(0));
    }

    [Fact]
    public void MaxStreamSize_throws_for_negative()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            builder.MaxStreamSize(-1));
    }

    [Fact]
    public void MaxStreamSize_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.MaxStreamSize(1000);

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void UseSearchStrategy_sets_strategy_type()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.UseSearchStrategy<TestSearchStrategy>();

        builder.GlobalSearchStrategyType.ShouldBe(typeof(TestSearchStrategy));
    }

    [Fact]
    public void UseSearchStrategy_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.UseSearchStrategy<TestSearchStrategy>();

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void GlobalSearchStrategyType_default_is_null()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.GlobalSearchStrategyType.ShouldBeNull();
    }

    [Fact]
    public void CursorPropertyName_default_is_null()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.CursorPropertyName.ShouldBeNull();
    }

    [Fact]
    public void DefaultSortValue_default_is_null()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        builder.DefaultSortValue.ShouldBeNull();
    }

    [Fact]
    public void SupportsCursorPagination_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.SupportsCursorPagination(e => e.Id);

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void DefaultSort_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.DefaultSort("-name");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void DateFilter_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.DateFilter(e => e.CreatedAt);

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void AllowGroupBy_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.AllowGroupBy(e => e.Age);

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void Aggregate_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.Aggregate(
            e => e.Age, AggregateFunction.Sum, "totalAge");

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void FilterGroup_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.FilterGroup(
            "Test", g => g.Preset("A", e => e.Age > 0));

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void FilterGroup_with_label_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.FilterGroup(
            "Test", "Label", g => g.Preset("A", e => e.Age > 0));

        result.ShouldBeSameAs(builder);
    }

    [Fact]
    public void GlobalSearch_chaining_returns_same_builder()
    {
        QueryDefinitionBuilder<TestEntity> builder = new();

        QueryDefinitionBuilder<TestEntity> result = builder.GlobalSearch(e => e.Name);

        result.ShouldBeSameAs(builder);
    }

    private sealed class TestSearchStrategy : IGlobalSearchStrategy<TestEntity>
    {
        public IQueryable<TestEntity> ApplySearch(
            IQueryable<TestEntity> source,
            string searchTerm,
            IReadOnlyList<string> searchProperties) =>
            source;
    }
}
