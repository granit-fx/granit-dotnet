using Granit.AI.Extraction;
using Granit.AI.Extraction.Internal;
using Granit.AI.Extraction.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.AI.Extraction.Tests;

public sealed record InvoiceData
{
    public string? Supplier { get; init; }
    public decimal Amount { get; init; }
    public string? Currency { get; init; }
}

public sealed class DefaultDocumentExtractorTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<ExtractionOptions> _options = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions
    {
        WorkspaceName = "test",
        ReviewThreshold = 0.7,
        TimeoutSeconds = 30,
    });

    private readonly DefaultDocumentExtractor<InvoiceData> _sut;

    public DefaultDocumentExtractorTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _sut = new DefaultDocumentExtractor<InvoiceData>(
            _chatClientFactory,
            _options,
            NullLogger<DefaultDocumentExtractor<InvoiceData>>.Instance);
    }

    [Fact]
    public async Task ExtractAsync_ValidResponse_ReturnsSuccess()
    {
        // Arrange
        const string jsonResponse = """{"supplier":"Acme Corp","amount":1500.50,"currency":"EUR"}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse))
            {
                FinishReason = ChatFinishReason.Stop,
            });

        // Act
        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync("Invoice from Acme Corp, total 1500.50 EUR", TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ExtractionStatus.Succeeded);
        result.Data.ShouldNotBeNull();
        result.Data.Supplier.ShouldBe("Acme Corp");
        result.Data.Amount.ShouldBe(1500.50m);
        result.Data.Currency.ShouldBe("EUR");
        result.ConfidenceScore.ShouldNotBeNull();
        result.ConfidenceScore.Value.ShouldBeGreaterThanOrEqualTo(0.7);
    }

    [Fact]
    public async Task ExtractAsync_LowConfidence_ReturnsNeedsReview()
    {
        // Arrange
        const string jsonResponse = """{"supplier":"Unknown","amount":0,"currency":"USD"}""";

        // No FinishReason.Stop and no confidence metadata -> default 0.75
        // But we need below threshold, so set threshold high
        IOptions<ExtractionOptions> highThresholdOptions = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions
        {
            WorkspaceName = "test",
            ReviewThreshold = 0.9,
            TimeoutSeconds = 30,
        });

        var extractor = new DefaultDocumentExtractor<InvoiceData>(
            _chatClientFactory,
            highThresholdOptions,
            NullLogger<DefaultDocumentExtractor<InvoiceData>>.Instance);

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        ExtractionResult<InvoiceData> result = await extractor.ExtractAsync("Some ambiguous document", TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ExtractionStatus.NeedsReview);
        result.Data.ShouldNotBeNull();
        result.Data.Supplier.ShouldBe("Unknown");
        result.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_LLMFailure_ReturnsFailed()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Provider unavailable"));

        // Act
        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync("Some document content", TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ExtractionStatus.Failed);
        result.Data.ShouldBeNull();
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("Provider unavailable");
    }

    [Fact]
    public async Task ExtractAsync_InvalidJson_ReturnsFailed()
    {
        // Arrange
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, "not valid json")));

        // Act
        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync("Some document", TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ExtractionStatus.Failed);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("deserialize");
    }

    [Fact]
    public async Task ExtractAsync_MarkdownCodeFences_StripsAndParses()
    {
        // Arrange
        const string jsonWithFences = """
            ```json
            {"supplier":"Fenced Corp","amount":200,"currency":"GBP"}
            ```
            """;

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonWithFences))
            {
                FinishReason = ChatFinishReason.Stop,
            });

        // Act
        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync("Document with fenced response", TestContext.Current.CancellationToken);

        // Assert
        result.Status.ShouldBe(ExtractionStatus.Succeeded);
        result.Data.ShouldNotBeNull();
        result.Data.Supplier.ShouldBe("Fenced Corp");
    }

    [Fact]
    public async Task ExtractAsync_NullContent_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() => _sut.ExtractAsync(null!, TestContext.Current.CancellationToken));
}
