using System.Text.Json;
using Granit.AI.Tools;
using Granit.AI.Tools.Extensions;
using Granit.QueryEngine.AI.Tools.Extensions;
using Granit.QueryEngine.AI.Tools.Internal;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Granit.QueryEngine.AI.Tools.Tests;

public sealed class QueryDataToolTests
{
    public sealed class Thing
    {
        public string Name { get; init; } = "";
        public int Amount { get; init; }
    }

    private static QueryMetadata Metadata() => new()
    {
        Columns = [],
        FilterableFields =
        [
            new FilterableField("name", "string", [FilterOperator.Eq, FilterOperator.Contains]),
            new FilterableField("amount", "number", [FilterOperator.Gte, FilterOperator.Lte]),
        ],
        SortableFields = [new SortableField("name"), new SortableField("amount")],
        PresetFilterGroups = [],
        QuickFilters = [],
        DateFilters = [],
        GroupByFields = [],
        Pagination = new PaginationMeta(20, 100, 1000, SupportsCursor: false),
    };

    private static (QueryDataTool<Thing> Tool, IQueryEngine<Thing> Engine, IQueryable<Thing> Source)
        CreateTool(PagedResult<Thing>? result = null)
    {
        IQueryEngine<Thing> engine = Substitute.For<IQueryEngine<Thing>>();
        engine.GetMetadata().Returns(Metadata());
        engine.ExecuteAsync(Arg.Any<IQueryable<Thing>>(), Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(result ?? new PagedResult<Thing>([], 0, false));

        IQueryable<Thing> queryable = new[] { new Thing { Name = "a", Amount = 1 } }.AsQueryable();
        IQueryableSource<Thing> source = Substitute.For<IQueryableSource<Thing>>();
        source.GetQueryable().Returns(queryable);

        return (new QueryDataTool<Thing>("things", null, engine, source), engine, queryable);
    }

    [Fact]
    public void Tool_name_is_prefixed_with_query()
    {
        (QueryDataTool<Thing> tool, _, _) = CreateTool();
        tool.Name.ShouldBe("query_things");
    }

    [Fact]
    public void Schema_projects_filterable_fields_and_operators()
    {
        (QueryDataTool<Thing> tool, _, _) = CreateTool();

        JsonElement itemProps = tool.ParameterSchema
            .GetProperty("properties").GetProperty("filters")
            .GetProperty("items").GetProperty("properties");

        string[] fields = [.. itemProps.GetProperty("field").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!)];
        string[] operators = [.. itemProps.GetProperty("operator").GetProperty("enum").EnumerateArray().Select(e => e.GetString()!)];

        fields.ShouldBe(["name", "amount"]);
        operators.ShouldBe(["eq", "contains", "gte", "lte"]);
        tool.ParameterSchema.GetProperty("properties").GetProperty("pageSize").GetProperty("maximum").GetInt32().ShouldBe(100);
    }

    [Fact]
    public async Task Maps_valid_filters_into_the_query_request_and_drops_unknown_ones()
    {
        (QueryDataTool<Thing> tool, IQueryEngine<Thing> engine, _) = CreateTool();

        JsonElement args = JsonSerializer.SerializeToElement(new
        {
            filters = new[]
            {
                new { field = "name", @operator = "contains", value = "abc" },
                new { field = "ghost", @operator = "eq", value = "x" },
            },
            pageSize = 5,
        });

        AIToolResult result = await tool.InvokeAsync(
            new AIToolInvocationContext { Arguments = args }, TestContext.Current.CancellationToken);

        await engine.Received(1).ExecuteAsync(
            Arg.Any<IQueryable<Thing>>(),
            Arg.Is<QueryRequest>(r =>
                r.Filter != null
                && r.Filter["name.contains"] == "abc"
                && !r.Filter.ContainsKey("ghost.eq")
                && r.PageSize == 5),
            Arg.Any<CancellationToken>());

        using var payload = JsonDocument.Parse(result.Content);
        payload.RootElement.GetProperty("ignoredFilters")[0].GetString().ShouldBe("ghost.eq");
    }

    [Fact]
    public async Task Clamps_page_size_to_the_definition_maximum()
    {
        (QueryDataTool<Thing> tool, IQueryEngine<Thing> engine, _) = CreateTool();

        JsonElement args = JsonSerializer.SerializeToElement(new { pageSize = 99999 });

        await tool.InvokeAsync(new AIToolInvocationContext { Arguments = args }, TestContext.Current.CancellationToken);

        await engine.Received(1).ExecuteAsync(
            Arg.Any<IQueryable<Thing>>(),
            Arg.Is<QueryRequest>(r => r.PageSize == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Executes_against_the_caller_scoped_queryable_source()
    {
        (QueryDataTool<Thing> tool, IQueryEngine<Thing> engine, IQueryable<Thing> source) = CreateTool();

        await tool.InvokeAsync(
            new AIToolInvocationContext { Arguments = JsonSerializer.SerializeToElement(new { }) },
            TestContext.Current.CancellationToken);

        // The ACL/tenant-scoped queryable from IQueryableSource is what gets executed.
        await engine.Received(1).ExecuteAsync(source, Arg.Any<QueryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Result_reports_entity_and_paging_metadata()
    {
        PagedResult<Thing> paged = new([new Thing { Name = "x", Amount = 7 }], TotalCount: 42, HasMore: true);
        (QueryDataTool<Thing> tool, _, _) = CreateTool(paged);

        AIToolResult result = await tool.InvokeAsync(
            new AIToolInvocationContext { Arguments = JsonSerializer.SerializeToElement(new { }) },
            TestContext.Current.CancellationToken);

        using var payload = JsonDocument.Parse(result.Content);
        JsonElement root = payload.RootElement;
        root.GetProperty("entity").GetString().ShouldBe("things");
        root.GetProperty("totalCount").GetInt32().ShouldBe(42);
        root.GetProperty("hasMore").GetBoolean().ShouldBeTrue();
        root.GetProperty("count").GetInt32().ShouldBe(1);
        root.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("x");
    }

    [Fact]
    public void Only_opted_in_definitions_are_exposed_as_tools()
    {
        IQueryEngine<Thing> engine = Substitute.For<IQueryEngine<Thing>>();
        engine.GetMetadata().Returns(Metadata());
        IQueryableSource<Thing> source = Substitute.For<IQueryableSource<Thing>>();

        ServiceCollection services = new();
        services.AddSingleton(engine);
        services.AddScoped(_ => source);
        services.AddGranitAITools(tools => tools.AddQueryData(q => q.Add<Thing>("things")));

        using ServiceProvider provider = services.BuildServiceProvider();
        using IServiceScope scope = provider.CreateScope();
        IAIToolRegistry registry = scope.ServiceProvider.GetRequiredService<IAIToolRegistry>();

        registry.TryGet("query_things", out _).ShouldBeTrue();
        registry.TryGet("query_orders", out _).ShouldBeFalse();
        registry.Tools.ShouldHaveSingleItem();
    }
}
