using Granit.AI;
using Granit.QueryEngine.AI.Internal;
using Granit.QueryEngine.AI.Options;
using Granit.QueryEngine.Meta;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.QueryEngine.AI.Tests;

public sealed class LlmNaturalLanguageQueryTranslatorAdditionalTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<QueryEngineAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new QueryEngineAIOptions());
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly LlmNaturalLanguageQueryTranslator _sut;

    public LlmNaturalLanguageQueryTranslatorAdditionalTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero));

        _sut = new LlmNaturalLanguageQueryTranslator(
            _structured,
            _options,
            NullLogger<LlmNaturalLanguageQueryTranslator>.Instance,
            _clock);
    }

    private void Respond(LlmQueryPayload payload) =>
        _structured
            .CompleteAsync<LlmQueryPayload>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmQueryPayload> { Status = StructuredCompletionStatus.Succeeded, Value = payload });

    [Fact]
    public void BuildInstruction_includes_date_filters()
    {
        string prompt = _sut.BuildInstruction(CreateMetadataWithDateFilters());

        prompt.ShouldContain("createdAt");
        prompt.ShouldContain("Today");
    }

    [Fact]
    public void BuildInstruction_includes_group_by_fields()
    {
        string prompt = _sut.BuildInstruction(CreateMetadataWithGroupBy());

        prompt.ShouldContain("category");
        prompt.ShouldContain("group-by");
    }

    [Fact]
    public void BuildInstruction_includes_sortable_fields()
    {
        string prompt = _sut.BuildInstruction(CreateMetadataWithSortableFields());

        prompt.ShouldContain("name");
        prompt.ShouldContain("createdAt");
        prompt.ShouldContain("Sort format");
    }

    [Fact]
    public void BuildInstruction_includes_current_date()
    {
        string prompt = _sut.BuildInstruction(CreateMinimalMetadata());

        prompt.ShouldContain("2026-03-21");
    }

    [Fact]
    public void BuildInstruction_includes_quick_filters()
    {
        string prompt = _sut.BuildInstruction(CreateMetadataWithQuickFilters());

        prompt.ShouldContain("active");
        prompt.ShouldContain("Active Items");
    }

    [Fact]
    public void BuildInstruction_empty_metadata_still_describes_the_output_fields_and_rules()
    {
        string prompt = _sut.BuildInstruction(CreateMinimalMetadata());

        prompt.ShouldContain("page");
        prompt.ShouldContain("filter");
        prompt.ShouldContain("Rules:");
    }

    [Fact]
    public async Task TranslateAsync_response_with_quickFilters_is_mapped()
    {
        Respond(new LlmQueryPayload { QuickFilters = ["active", "recent"] });

        QueryMetadata metadata = CreateMinimalMetadata() with
        {
            QuickFilters =
            [
                new QuickFilterMeta("active", "Active Items", false),
                new QuickFilterMeta("recent", "Recent Items", false),
            ],
        };

        QueryRequest? result = await _sut.TranslateAsync(
            "show active recent items", metadata, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.QuickFilters.ShouldNotBeNull();
        result.QuickFilters.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TranslateAsync_response_with_empty_filter_maps_to_null()
    {
        Respond(new LlmQueryPayload { Filter = [], QuickFilters = [] });

        QueryRequest? result = await _sut.TranslateAsync(
            "show all", CreateMinimalMetadata(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Filter.ShouldBeNull();
        result.QuickFilters.ShouldBeNull();
    }

    [Fact]
    public async Task TranslateAsync_response_with_groupBy_is_mapped()
    {
        Respond(new LlmQueryPayload { GroupBy = "category" });

        QueryMetadata metadata = CreateMinimalMetadata() with
        {
            GroupByFields = [new GroupByField("category", "String")],
        };

        QueryRequest? result = await _sut.TranslateAsync(
            "group by category", metadata, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.GroupBy.ShouldBe("category");
    }

    [Fact]
    public async Task TranslateAsync_response_with_page_and_pageSize_is_mapped()
    {
        Respond(new LlmQueryPayload { Page = 3, PageSize = 50 });

        QueryRequest? result = await _sut.TranslateAsync(
            "page 3 with 50 items", CreateMinimalMetadata(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Page.ShouldBe(3);
        result.PageSize.ShouldBe(50);
    }

    private static QueryMetadata CreateMinimalMetadata() =>
        new()
        {
            Columns = [],
            FilterableFields = [],
            SortableFields = [],
            PresetFilterGroups = [],
            QuickFilters = [],
            DateFilters = [],
            GroupByFields = [],
            Pagination = new PaginationMeta(20, 100, QueryEngineDefaults.MaxStreamSize, false),
        };

    private static QueryMetadata CreateMetadataWithDateFilters() =>
        CreateMinimalMetadata() with
        {
            DateFilters =
            [
                new DateFilterMeta("createdAt", DatePeriod.ThisMonth,
                    [DatePeriod.Today, DatePeriod.ThisWeek, DatePeriod.ThisMonth]),
            ],
        };

    private static QueryMetadata CreateMetadataWithGroupBy() =>
        CreateMinimalMetadata() with
        {
            GroupByFields = [new GroupByField("category", "String")],
        };

    private static QueryMetadata CreateMetadataWithSortableFields() =>
        CreateMinimalMetadata() with
        {
            SortableFields =
            [
                new SortableField("name"),
                new SortableField("createdAt"),
            ],
        };

    private static QueryMetadata CreateMetadataWithQuickFilters() =>
        CreateMinimalMetadata() with
        {
            QuickFilters = [new QuickFilterMeta("active", "Active Items", false)],
        };
}
