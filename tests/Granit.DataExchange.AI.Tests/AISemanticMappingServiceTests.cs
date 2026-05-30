using Granit.AI;
using Granit.DataExchange.AI.Internal;
using Granit.DataExchange.AI.Options;
using Granit.DataExchange.AI.Schema;
using Granit.DataExchange.Import;
using Granit.DataExchange.Import.Mapping;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Granit.DataExchange.AI.Tests;

public sealed class AISemanticMappingServiceTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();

    private readonly DataExchangeAIOptions _options = new()
    {
        WorkspaceName = "test-workspace",
        TimeoutSeconds = 10,
        MinConfidenceScore = 0.6,
    };

    private readonly IReadOnlyList<string> _headers = ["Email", "Full Name", "Phone Number"];

    private readonly IReadOnlyList<ImportFieldMetadata> _targetFields =
    [
        new("Email", "String", "Email Address", "The user's email", true),
        new("FullName", "String", "Full Name", "The user's full name", true),
        new("PhoneNumber", "String", "Phone", "Party phone number", false),
    ];

    private AISemanticMappingService CreateService() =>
        new(_structured, Microsoft.Extensions.Options.Options.Create(_options), NullLogger<AISemanticMappingService>.Instance);

    private static StructuredCompletionResult<MappingSuggestionsResponse> Ok(params (string Source, string Target, double Score)[] items) =>
        new()
        {
            Status = StructuredCompletionStatus.Succeeded,
            Value = new MappingSuggestionsResponse
            {
                Mappings = [.. items.Select(i => new MappingSuggestionItem { Source = i.Source, Target = i.Target, Score = i.Score })],
            },
        };

    private void Respond(StructuredCompletionResult<MappingSuggestionsResponse> result) =>
        _structured
            .CompleteAsync<MappingSuggestionsResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public void IsAvailable_ReturnsTrue() => CreateService().IsAvailable.ShouldBeTrue();

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithValidResponse_ReturnsMappingsSortedByScoreDescending()
    {
        Respond(Ok(("Full Name", "FullName", 0.90), ("Email", "Email", 0.95), ("Phone Number", "PhoneNumber", 0.85)));

        IReadOnlyList<SemanticMappingSuggestion> result = await CreateService()
            .SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
        result[0].SourceColumn.ShouldBe("Email");
        result[0].TargetProperty.ShouldBe("Email");
        result[0].Score.ShouldBe(0.95);
        result[0].Score.ShouldBeGreaterThanOrEqualTo(result[1].Score);
        result[1].Score.ShouldBeGreaterThanOrEqualTo(result[2].Score);
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_FiltersLowConfidence()
    {
        Respond(Ok(("Email", "Email", 0.95), ("Full Name", "FullName", 0.40), ("Phone Number", "PhoneNumber", 0.55)));

        IReadOnlyList<SemanticMappingSuggestion> result = await CreateService()
            .SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].SourceColumn.ShouldBe("Email");
        result[0].Score.ShouldBe(0.95);
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_DropsMappingsOutsideTheKnownSchema()
    {
        // Model invents a source column and a target property nobody supplied — both discarded.
        Respond(Ok(("Email", "Email", 0.95), ("Ghost Column", "FullName", 0.99), ("Phone Number", "GhostProperty", 0.99)));

        IReadOnlyList<SemanticMappingSuggestion> result = await CreateService()
            .SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].SourceColumn.ShouldBe("Email");
    }

    [Theory]
    [InlineData(StructuredCompletionStatus.ModelRefused)]
    [InlineData(StructuredCompletionStatus.SchemaViolation)]
    [InlineData(StructuredCompletionStatus.TransportFailure)]
    public async Task SuggestSemanticMappingsAsync_NonSuccessStatus_ReturnsEmptyList(StructuredCompletionStatus status)
    {
        Respond(new StructuredCompletionResult<MappingSuggestionsResponse> { Status = status });

        IReadOnlyList<SemanticMappingSuggestion> result = await CreateService()
            .SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithEmptyHeaders_ReturnsEmptyWithoutCallingTheModel()
    {
        IReadOnlyList<SemanticMappingSuggestion> result = await CreateService()
            .SuggestSemanticMappingsAsync([], _targetFields, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        await _structured
            .DidNotReceiveWithAnyArgs()
            .CompleteAsync<MappingSuggestionsResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithEmptyTargetFields_ReturnsEmptyWithoutCallingTheModel()
    {
        IReadOnlyList<SemanticMappingSuggestion> result = await CreateService()
            .SuggestSemanticMappingsAsync(_headers, [], TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        await _structured
            .DidNotReceiveWithAnyArgs()
            .CompleteAsync<MappingSuggestionsResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_RoutesHeadersAndTargetSchemaThroughTheRightChannels()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<MappingSuggestionsResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Ok(("Email", "Email", 0.95)));

        await CreateService().SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        // Untrusted source columns flow through the sanitized Content channel...
        captured.Content.ShouldContain("Email");
        captured.Content.ShouldContain("Phone Number");
        // ...the developer-controlled target schema is the instruction.
        captured.Instruction.ShouldNotBeNull();
        captured.Instruction.ShouldContain("FullName");
        captured.Instruction.ShouldContain("Email Address");
        captured.WorkspaceName.ShouldBe("test-workspace");
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_PreviewRows_OmittedFromContentWhenOptionDisabled()
    {
        _options.IncludePreviewRows = false;
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<MappingSuggestionsResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Ok(("COL1", "Email", 0.9)));

        IReadOnlyList<string[]> previewRows = [["john@example.com"]];

        IReadOnlyList<SemanticMappingSuggestion> result = await CreateService()
            .SuggestSemanticMappingsAsync(["COL1"], _targetFields, previewRows, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        captured.ShouldNotBeNull();
        captured.Content.ShouldNotContain("Sample data");
        captured.Content.ShouldNotContain("john@example.com");
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_PreviewRows_IncludedInContentWhenOptionEnabled()
    {
        _options.IncludePreviewRows = true;
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<MappingSuggestionsResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Ok(("COL1", "Email", 0.9)));

        IReadOnlyList<string[]> previewRows = [["john@example.com"]];

        IReadOnlyList<SemanticMappingSuggestion> result = await CreateService()
            .SuggestSemanticMappingsAsync(["COL1"], _targetFields, previewRows, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        captured.ShouldNotBeNull();
        captured.Content.ShouldContain("Sample data");
        captured.Content.ShouldContain("john@example.com");
    }
}
