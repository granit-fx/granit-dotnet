using Granit.AI;
using Granit.Querying.AI.Internal;
using Granit.Querying.AI.Options;
using Granit.Querying.Filtering;
using Granit.Querying.Meta;
using Granit.Timing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Querying.AI.Tests;

public sealed class LlmNaturalLanguageQueryTranslatorAdditionalTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<QueryingAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new QueryingAIOptions());
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly LlmNaturalLanguageQueryTranslator _sut;

    public LlmNaturalLanguageQueryTranslatorAdditionalTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _clock.Now.Returns(new DateTimeOffset(2026, 3, 21, 0, 0, 0, TimeSpan.Zero));

        _sut = new LlmNaturalLanguageQueryTranslator(
            _chatClientFactory,
            _options,
            NullLogger<LlmNaturalLanguageQueryTranslator>.Instance,
            _clock);
    }

    [Fact]
    public void BuildSystemPrompt_includes_date_filters()
    {
        QueryMetadata metadata = CreateMetadataWithDateFilters();

        string prompt = _sut.BuildSystemPrompt(metadata);

        prompt.ShouldContain("createdAt");
        prompt.ShouldContain("Today");
    }

    [Fact]
    public void BuildSystemPrompt_includes_group_by_fields()
    {
        QueryMetadata metadata = CreateMetadataWithGroupBy();

        string prompt = _sut.BuildSystemPrompt(metadata);

        prompt.ShouldContain("category");
        prompt.ShouldContain("group-by");
    }

    [Fact]
    public void BuildSystemPrompt_includes_sortable_fields()
    {
        QueryMetadata metadata = CreateMetadataWithSortableFields();

        string prompt = _sut.BuildSystemPrompt(metadata);

        prompt.ShouldContain("name");
        prompt.ShouldContain("createdAt");
        prompt.ShouldContain("Sort format");
    }

    [Fact]
    public void BuildSystemPrompt_includes_current_date()
    {
        QueryMetadata metadata = CreateMinimalMetadata();

        string prompt = _sut.BuildSystemPrompt(metadata);

        prompt.ShouldContain("2026-03-21");
    }

    [Fact]
    public void BuildSystemPrompt_includes_quick_filters()
    {
        QueryMetadata metadata = CreateMetadataWithQuickFilters();

        string prompt = _sut.BuildSystemPrompt(metadata);

        prompt.ShouldContain("active");
        prompt.ShouldContain("Active Items");
    }

    [Fact]
    public void BuildSystemPrompt_empty_metadata_includes_schema()
    {
        QueryMetadata metadata = CreateMinimalMetadata();

        string prompt = _sut.BuildSystemPrompt(metadata);

        prompt.ShouldContain("page");
        prompt.ShouldContain("filter");
        prompt.ShouldContain("Rules:");
    }

    [Fact]
    public async Task TranslateAsync_response_with_quickFilters_is_mapped()
    {
        const string jsonText = """
            {
                "quickFilters": ["active", "recent"]
            }
            """;

        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, jsonText));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

        QueryMetadata metadata = CreateMinimalMetadata();

        QueryRequest? result = await _sut.TranslateAsync(
            "show active recent items",
            metadata,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.QuickFilters.ShouldNotBeNull();
        result.QuickFilters.Count.ShouldBe(2);
    }

    [Fact]
    public async Task TranslateAsync_response_with_empty_filter_maps_to_null()
    {
        const string jsonText = """
            {
                "filter": {},
                "quickFilters": []
            }
            """;

        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, jsonText));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

        QueryMetadata metadata = CreateMinimalMetadata();

        QueryRequest? result = await _sut.TranslateAsync(
            "show all",
            metadata,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Filter.ShouldBeNull();
        result.QuickFilters.ShouldBeNull();
    }

    [Fact]
    public void StripMarkdownFences_handles_bare_backticks_without_json_suffix()
    {
        const string input = """
            ```
            {"sort": "name"}
            ```
            """;

        string result = LlmNaturalLanguageQueryTranslator.StripMarkdownFences(input);

        result.ShouldBe("{\"sort\": \"name\"}");
    }

    [Fact]
    public async Task TranslateAsync_response_with_groupBy_is_mapped()
    {
        const string jsonText = """
            {
                "groupBy": "category"
            }
            """;

        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, jsonText));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

        QueryMetadata metadata = CreateMinimalMetadata();

        QueryRequest? result = await _sut.TranslateAsync(
            "group by category",
            metadata,
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.GroupBy.ShouldBe("category");
    }

    [Fact]
    public async Task TranslateAsync_response_with_page_and_pageSize_is_mapped()
    {
        const string jsonText = """
            {
                "page": 3,
                "pageSize": 50
            }
            """;

        ChatResponse response = new(new ChatMessage(ChatRole.Assistant, jsonText));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

        QueryMetadata metadata = CreateMinimalMetadata();

        QueryRequest? result = await _sut.TranslateAsync(
            "page 3 with 50 items",
            metadata,
            TestContext.Current.CancellationToken);

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
            Pagination = new PaginationMeta(20, 100, QueryingDefaults.MaxStreamSize, false),
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
