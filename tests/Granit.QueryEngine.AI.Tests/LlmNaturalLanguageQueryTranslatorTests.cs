using Granit.AI;
using Granit.QueryEngine.AI.Internal;
using Granit.QueryEngine.AI.Options;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;
using Granit.Timing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.QueryEngine.AI.Tests;

public sealed class LlmNaturalLanguageQueryTranslatorTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<QueryEngineAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new QueryEngineAIOptions());
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly LlmNaturalLanguageQueryTranslator _sut;

    public LlmNaturalLanguageQueryTranslatorTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _clock.Now.Returns(new DateTimeOffset(2026, 3, 16, 0, 0, 0, TimeSpan.Zero));

        _sut = new LlmNaturalLanguageQueryTranslator(
            _chatClientFactory,
            _options,
            NullLogger<LlmNaturalLanguageQueryTranslator>.Instance,
            _clock);
    }

    [Fact]
    public async Task TranslateAsync_ValidResponse_ReturnsQueryRequest()
    {
        // Arrange
        const string jsonText = """
            {
                "sort": "-createdAt",
                "filter": {
                    "status.eq": "active",
                    "amount.gte": "1000"
                }
            }
            """;

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonText));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

        QueryMetadata metadata = CreateTestMetadata();

        // Act
        QueryRequest? result = await _sut.TranslateAsync(
            "show active items over 1000, newest first",
            metadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Sort.ShouldBe("-createdAt");
        result.Filter.ShouldNotBeNull();
        result.Filter.Count.ShouldBe(2);
        result.Filter["status.eq"].ShouldBe("active");
        result.Filter["amount.gte"].ShouldBe("1000");
    }

    [Fact]
    public async Task TranslateAsync_LLMFailure_ReturnsNull()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        QueryMetadata metadata = CreateTestMetadata();

        // Act
        QueryRequest? result = await _sut.TranslateAsync(
            "show all items",
            metadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task TranslateAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "this is not valid json at all"));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

        QueryMetadata metadata = CreateTestMetadata();

        // Act
        QueryRequest? result = await _sut.TranslateAsync(
            "show all items",
            metadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task TranslateAsync_EmptyInput_ReturnsNull()
    {
        // Arrange
        QueryMetadata metadata = CreateTestMetadata();

        // Act
        QueryRequest? result = await _sut.TranslateAsync(
            "",
            metadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task TranslateAsync_MarkdownFencedResponse_ParsesCorrectly()
    {
        // Arrange
        const string jsonText = """
            ```json
            {
                "sort": "name",
                "filter": { "name.contains": "alice" }
            }
            ```
            """;

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonText));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);

        QueryMetadata metadata = CreateTestMetadata();

        // Act
        QueryRequest? result = await _sut.TranslateAsync(
            "find alice",
            metadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldNotBeNull();
        result.Sort.ShouldBe("name");
        result.Filter.ShouldNotBeNull();
        result.Filter["name.contains"].ShouldBe("alice");
    }

    [Fact]
    public async Task TranslateAsync_WhitespaceInput_ReturnsNull()
    {
        // Arrange
        QueryMetadata metadata = CreateTestMetadata();

        // Act
        QueryRequest? result = await _sut.TranslateAsync(
            "   ",
            metadata,
            TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public void BuildSystemPrompt_IncludesFilterableFields()
    {
        // Arrange
        QueryMetadata metadata = CreateTestMetadata();

        // Act
        string prompt = _sut.BuildSystemPrompt(metadata);

        // Assert
        prompt.ShouldContain("status");
        prompt.ShouldContain("amount");
        prompt.ShouldContain("eq");
        prompt.ShouldContain("gte");
    }

    [Fact]
    public void StripMarkdownFences_RemovesFences()
    {
        const string input = """
            ```json
            {"sort": "name"}
            ```
            """;

        string result = LlmNaturalLanguageQueryTranslator.StripMarkdownFences(input);

        result.ShouldBe("{\"sort\": \"name\"}");
    }

    [Fact]
    public void StripMarkdownFences_PlainJsonUnchanged()
    {
        const string input = """{"sort": "name"}""";

        string result = LlmNaturalLanguageQueryTranslator.StripMarkdownFences(input);

        result.ShouldBe("{\"sort\": \"name\"}");
    }

    private static QueryMetadata CreateTestMetadata() =>
        new()
        {
            FilterableFields =
            [
                new FilterableField("status", "string", [FilterOperator.Eq, FilterOperator.Contains]),
                new FilterableField("amount", "decimal", [FilterOperator.Gte, FilterOperator.Lte]),
                new FilterableField("name", "string", [FilterOperator.Contains]),
            ],
            SortableFields =
            [
                new SortableField("name"),
                new SortableField("createdAt"),
                new SortableField("amount"),
            ],
            QuickFilters =
            [
                new QuickFilterMeta("active", "Active Items", false),
            ],
            DateFilters = [],
            GroupByFields = [],
            Columns = [],
            PresetFilterGroups = [],
            Pagination = new PaginationMeta(25, 100, QueryEngineDefaults.MaxStreamSize, false),
        };
}
