using Granit.AI;
using Granit.QueryEngine.AI.Internal;
using Granit.QueryEngine.AI.Options;
using Granit.QueryEngine.Filtering;
using Granit.QueryEngine.Meta;
using Granit.Timing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.QueryEngine.AI.Tests;

public sealed class LlmNaturalLanguageQueryTranslatorTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<QueryEngineAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new QueryEngineAIOptions());
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly LlmNaturalLanguageQueryTranslator _sut;

    public LlmNaturalLanguageQueryTranslatorTests()
    {
        _clock.Now.Returns(new DateTimeOffset(2026, 3, 16, 0, 0, 0, TimeSpan.Zero));

        _sut = new LlmNaturalLanguageQueryTranslator(
            _structured,
            _options,
            NullLogger<LlmNaturalLanguageQueryTranslator>.Instance,
            _clock);
    }

    internal static List<LlmFilterClause> Clauses(params (string Key, string Value)[] clauses) =>
        [.. clauses.Select(c => new LlmFilterClause { Key = c.Key, Value = c.Value })];

    private void Respond(LlmQueryPayload payload) =>
        _structured
            .CompleteAsync<LlmQueryPayload>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmQueryPayload> { Status = StructuredCompletionStatus.Succeeded, Value = payload });

    private void RespondWith(StructuredCompletionStatus status) =>
        _structured
            .CompleteAsync<LlmQueryPayload>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmQueryPayload> { Status = status });

    [Fact]
    public async Task TranslateAsync_ValidResponse_ReturnsQueryRequest()
    {
        Respond(new LlmQueryPayload
        {
            Sort = "-createdAt",
            Filter = Clauses(("status.eq", "active"), ("amount.gte", "1000")),
        });

        QueryRequest? result = await _sut.TranslateAsync(
            "show active items over 1000, newest first", CreateTestMetadata(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Sort.ShouldBe("-createdAt");
        result.Filter.ShouldNotBeNull();
        result.Filter.Count.ShouldBe(2);
        result.Filter["status.eq"].ShouldBe("active");
        result.Filter["amount.gte"].ShouldBe("1000");
    }

    [Theory]
    [InlineData(StructuredCompletionStatus.ModelRefused)]
    [InlineData(StructuredCompletionStatus.SchemaViolation)]
    [InlineData(StructuredCompletionStatus.TransportFailure)]
    public async Task TranslateAsync_NonSuccessStatus_ReturnsNull(StructuredCompletionStatus status)
    {
        RespondWith(status);

        QueryRequest? result = await _sut.TranslateAsync(
            "show all items", CreateTestMetadata(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task TranslateAsync_StripsFilterClausesOutsideTheMetadataWhitelist()
    {
        // "ssn.eq" is not a filterable field — the model cannot smuggle it past validation (LLM02).
        Respond(new LlmQueryPayload
        {
            Filter = Clauses(("status.eq", "active"), ("ssn.eq", "123-45-6789")),
        });

        QueryRequest? result = await _sut.TranslateAsync(
            "active items", CreateTestMetadata(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Filter.ShouldNotBeNull();
        result.Filter.Count.ShouldBe(1);
        result.Filter.ContainsKey("status.eq").ShouldBeTrue();
        result.Filter.ContainsKey("ssn.eq").ShouldBeFalse();
    }

    [Fact]
    public async Task TranslateAsync_CollapsesDuplicateFilterClauseKeys()
    {
        Respond(new LlmQueryPayload
        {
            Filter = Clauses(("status.eq", "active"), ("status.eq", "inactive")),
        });

        QueryRequest? result = await _sut.TranslateAsync(
            "active items", CreateTestMetadata(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Filter.ShouldNotBeNull();
        result.Filter.Count.ShouldBe(1);
        result.Filter["status.eq"].ShouldBe("active");
    }

    [Fact]
    public async Task TranslateAsync_EmptyInput_ReturnsNullWithoutCallingTheModel()
    {
        QueryRequest? result = await _sut.TranslateAsync("", CreateTestMetadata(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _structured
            .DidNotReceiveWithAnyArgs()
            .CompleteAsync<LlmQueryPayload>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TranslateAsync_WhitespaceInput_ReturnsNullWithoutCallingTheModel()
    {
        QueryRequest? result = await _sut.TranslateAsync("   ", CreateTestMetadata(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
        await _structured
            .DidNotReceiveWithAnyArgs()
            .CompleteAsync<LlmQueryPayload>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task TranslateAsync_RoutesQueryAsContentAndSchemaAsInstruction()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<LlmQueryPayload>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmQueryPayload> { Status = StructuredCompletionStatus.Succeeded, Value = new LlmQueryPayload() });

        await _sut.TranslateAsync("find alice", CreateTestMetadata(), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        // The untrusted phrase is the content; the field schema is developer-controlled instruction.
        captured.Content.ShouldBe("find alice");
        captured.Instruction.ShouldNotBeNull();
        captured.Instruction.ShouldContain("status");
        captured.Instruction.ShouldContain("amount");
    }

    [Fact]
    public void BuildInstruction_IncludesFilterableFields()
    {
        string prompt = _sut.BuildInstruction(CreateTestMetadata());

        prompt.ShouldContain("status");
        prompt.ShouldContain("amount");
        prompt.ShouldContain("eq");
        prompt.ShouldContain("gte");
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
