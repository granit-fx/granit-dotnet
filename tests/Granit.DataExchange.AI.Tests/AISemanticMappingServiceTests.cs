using Granit.AI;
using Granit.DataExchange.AI.Internal;
using Granit.DataExchange.AI.Options;
using Granit.DataExchange.Import.Mapping;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.DataExchange.AI.Tests;

public sealed class AISemanticMappingServiceTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly ILogger<AISemanticMappingService> _logger = NullLogger<AISemanticMappingService>.Instance;

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
        new("PhoneNumber", "String", "Phone", "Contact phone number", false),
    ];

    private AISemanticMappingService CreateService() =>
        new(_chatClientFactory, Microsoft.Extensions.Options.Options.Create(_options), _logger);

    private void SetupChatClient(string jsonResponse)
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse));
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(response);
    }

    [Fact]
    public void IsAvailable_ReturnsTrue()
    {
        AISemanticMappingService service = CreateService();

        service.IsAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithValidResponse_ReturnsMappings()
    {
        const string jsonResponse = """
            [
                {"source": "Email", "target": "Email", "score": 0.95},
                {"source": "Full Name", "target": "FullName", "score": 0.90},
                {"source": "Phone Number", "target": "PhoneNumber", "score": 0.85}
            ]
            """;

        SetupChatClient(jsonResponse);

        AISemanticMappingService service = CreateService();

        IReadOnlyList<SemanticMappingSuggestion> result = await service
            .SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(3);
        result[0].SourceColumn.ShouldBe("Email");
        result[0].TargetProperty.ShouldBe("Email");
        result[0].Score.ShouldBe(0.95);
        result[1].SourceColumn.ShouldBe("Full Name");
        result[1].TargetProperty.ShouldBe("FullName");
        result[2].SourceColumn.ShouldBe("Phone Number");
        result[2].TargetProperty.ShouldBe("PhoneNumber");

        // Should be sorted by score descending
        result[0].Score.ShouldBeGreaterThanOrEqualTo(result[1].Score);
        result[1].Score.ShouldBeGreaterThanOrEqualTo(result[2].Score);
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithLLMFailure_ReturnsEmptyList()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM provider unavailable"));

        AISemanticMappingService service = CreateService();

        IReadOnlyList<SemanticMappingSuggestion> result = await service
            .SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_FiltersLowConfidence()
    {
        const string jsonResponse = """
            [
                {"source": "Email", "target": "Email", "score": 0.95},
                {"source": "Full Name", "target": "FullName", "score": 0.40},
                {"source": "Phone Number", "target": "PhoneNumber", "score": 0.55}
            ]
            """;

        SetupChatClient(jsonResponse);

        AISemanticMappingService service = CreateService();

        IReadOnlyList<SemanticMappingSuggestion> result = await service
            .SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].SourceColumn.ShouldBe("Email");
        result[0].Score.ShouldBe(0.95);
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithEmptyHeaders_ReturnsEmptyList()
    {
        AISemanticMappingService service = CreateService();

        IReadOnlyList<SemanticMappingSuggestion> result = await service
            .SuggestSemanticMappingsAsync([], _targetFields, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithEmptyTargetFields_ReturnsEmptyList()
    {
        AISemanticMappingService service = CreateService();

        IReadOnlyList<SemanticMappingSuggestion> result = await service
            .SuggestSemanticMappingsAsync(_headers, [], TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithMarkdownFencedResponse_ParsesCorrectly()
    {
        const string jsonResponse = """
            ```json
            [
                {"source": "Email", "target": "Email", "score": 0.95}
            ]
            ```
            """;

        SetupChatClient(jsonResponse);

        AISemanticMappingService service = CreateService();

        IReadOnlyList<SemanticMappingSuggestion> result = await service
            .SuggestSemanticMappingsAsync(_headers, _targetFields, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].SourceColumn.ShouldBe("Email");
    }

    [Fact]
    public void BuildPrompt_ContainsHeadersAndFields()
    {
        string prompt = AISemanticMappingService.BuildPrompt(_headers, _targetFields, 0.6);

        prompt.ShouldContain("Email, Full Name, Phone Number");
        prompt.ShouldContain("Email Address");
        prompt.ShouldContain("The user's email");
        prompt.ShouldContain("FullName");
        prompt.ShouldContain("PhoneNumber");
        prompt.ShouldContain("0.6");
    }

    [Fact]
    public void BuildPrompt_WithPreviewRows_IncludesSampleData()
    {
        IReadOnlyList<string[]> previewRows =
        [
            ["john@example.com", "John Doe", "+32 123 456"],
            ["jane@example.com", "Jane Smith", "+32 789 012"],
        ];

        string prompt = AISemanticMappingService.BuildPrompt(_headers, _targetFields, 0.6, previewRows);

        prompt.ShouldContain("Sample data (first rows):");
        prompt.ShouldContain("john@example.com");
        prompt.ShouldContain("Jane Smith");
    }

    [Fact]
    public void BuildPrompt_WithoutPreviewRows_DoesNotContainSampleData()
    {
        string prompt = AISemanticMappingService.BuildPrompt(_headers, _targetFields, 0.6, previewRows: null);

        prompt.ShouldNotContain("Sample data");
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithPreviewRows_IgnoredWhenOptionDisabled()
    {
        _options.IncludePreviewRows = false;

        const string jsonResponse = """[{"source": "COL1", "target": "Email", "score": 0.9}]""";
        SetupChatClient(jsonResponse);

        AISemanticMappingService service = CreateService();
        IReadOnlyList<string[]> previewRows = [["john@example.com"]];

        IReadOnlyList<SemanticMappingSuggestion> result = await service
            .SuggestSemanticMappingsAsync(["COL1"], _targetFields, previewRows, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);

        // Verify the prompt does NOT contain sample data (option is disabled)
        await _chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs =>
                !string.Join("", msgs.Select(m => m.Text)).Contains("Sample data")),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SuggestSemanticMappingsAsync_WithPreviewRows_IncludedWhenOptionEnabled()
    {
        _options.IncludePreviewRows = true;

        const string jsonResponse = """[{"source": "COL1", "target": "Email", "score": 0.9}]""";
        SetupChatClient(jsonResponse);

        AISemanticMappingService service = CreateService();
        IReadOnlyList<string[]> previewRows = [["john@example.com"]];

        IReadOnlyList<SemanticMappingSuggestion> result = await service
            .SuggestSemanticMappingsAsync(["COL1"], _targetFields, previewRows, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);

        // Verify the prompt DOES contain sample data
        await _chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs =>
                string.Join("", msgs.Select(m => m.Text)).Contains("Sample data")),
            Arg.Any<ChatOptions?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ParseSuggestions_WithInvalidJson_ReturnsEmptyList()
    {
        IReadOnlyList<SemanticMappingSuggestion> result =
            AISemanticMappingService.ParseSuggestions("not valid json");

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseSuggestions_WithEmptyArray_ReturnsEmptyList()
    {
        IReadOnlyList<SemanticMappingSuggestion> result =
            AISemanticMappingService.ParseSuggestions("[]");

        result.ShouldBeEmpty();
    }
}
