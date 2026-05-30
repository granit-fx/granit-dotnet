using Granit.AI.Extraction.Internal;
using Granit.AI.Extraction.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
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
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();

    private readonly IOptions<ExtractionOptions> _options = Microsoft.Extensions.Options.Options.Create(new ExtractionOptions
    {
        WorkspaceName = "test",
        ReviewThreshold = 0.7,
    });

    // Typed as the interface so ExtractAsync(string) resolves to the default-interface-method.
    private readonly IDocumentExtractor<InvoiceData> _sut;

    public DefaultDocumentExtractorTests() =>
        _sut = new DefaultDocumentExtractor<InvoiceData>(_structured, _options, NullLogger<DefaultDocumentExtractor<InvoiceData>>.Instance);

    private static readonly InvoiceData SampleInvoice = new() { Supplier = "Acme Corp", Amount = 1500.50m, Currency = "EUR" };

    private void CompletionReturns(StructuredCompletionResult<InvoiceData> result) =>
        _structured
            .CompleteAsync<InvoiceData>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

    private static StructuredCompletionResult<InvoiceData> Succeeded(
        ChatFinishReason? finishReason = null,
        IReadOnlyDictionary<string, object?>? metadata = null,
        string? modelId = null) =>
        new()
        {
            Status = StructuredCompletionStatus.Succeeded,
            Value = SampleInvoice,
            FinishReason = finishReason ?? ChatFinishReason.Stop,
            Metadata = metadata,
            ModelId = modelId,
        };

    [Fact]
    public async Task ExtractAsync_Succeeded_ReturnsSuccessWithModelId()
    {
        CompletionReturns(Succeeded(modelId: "gpt-4o-mini-2024-07-18"));

        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync(
            new ExtractionRequest { Content = "Invoice from Acme Corp" }, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ExtractionStatus.Succeeded);
        result.Data.ShouldBe(SampleInvoice);
        result.ConfidenceScore.ShouldNotBeNull();
        result.ConfidenceScore.Value.ShouldBeGreaterThanOrEqualTo(0.7);
        result.ModelId.ShouldBe("gpt-4o-mini-2024-07-18");
    }

    [Fact]
    public async Task ExtractAsync_LowConfidenceMetadata_ReturnsNeedsReview()
    {
        CompletionReturns(Succeeded(metadata: new Dictionary<string, object?> { ["confidence"] = 0.5 }));

        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync(
            new ExtractionRequest { Content = "ambiguous" }, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ExtractionStatus.NeedsReview);
        result.Data.ShouldBe(SampleInvoice);
        result.ConfidenceScore.ShouldBe(0.5);
        result.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ExtractAsync_ModelRefused_ReturnsFailed()
    {
        CompletionReturns(new StructuredCompletionResult<InvoiceData> { Status = StructuredCompletionStatus.ModelRefused });

        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync(
            new ExtractionRequest { Content = "x" }, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ExtractionStatus.Failed);
        result.Data.ShouldBeNull();
    }

    [Fact]
    public async Task ExtractAsync_SchemaViolation_ReturnsFailed_WithDeserializeMessage()
    {
        CompletionReturns(new StructuredCompletionResult<InvoiceData> { Status = StructuredCompletionStatus.SchemaViolation });

        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync(
            new ExtractionRequest { Content = "x" }, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ExtractionStatus.Failed);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage.ShouldContain("deserialize");
    }

    [Fact]
    public async Task ExtractAsync_TransportFailure_ReturnsFailed_WithPrimitiveSafeMessage()
    {
        CompletionReturns(new StructuredCompletionResult<InvoiceData>
        {
            Status = StructuredCompletionStatus.TransportFailure,
            ErrorMessage = "The AI request timed out after 30 seconds.",
        });

        ExtractionResult<InvoiceData> result = await _sut.ExtractAsync(
            new ExtractionRequest { Content = "x" }, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ExtractionStatus.Failed);
        result.ErrorMessage.ShouldBe("The AI request timed out after 30 seconds.");
    }

    [Fact]
    public async Task ExtractAsync_PassesRequestThroughToPrimitive_WithWorkspace()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<InvoiceData>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Succeeded());

        await _sut.ExtractAsync(
            new ExtractionRequest
            {
                Instruction = "Generate SEO metadata in French.",
                Content = "Page body.",
                ContentLabel = "Page content",
                Context = [new("Title", "Home")],
            },
            TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Instruction.ShouldBe("Generate SEO metadata in French.");
        captured.Content.ShouldBe("Page body.");
        captured.ContentLabel.ShouldBe("Page content");
        captured.Context.ShouldNotBeNull();
        captured.WorkspaceName.ShouldBe("test"); // from ExtractionOptions
    }

    [Fact]
    public async Task ExtractAsync_StringOverload_DelegatesToRequestOverload()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<InvoiceData>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Succeeded());

        await _sut.ExtractAsync("Raw document text", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.ShouldBe("Raw document text");
        captured.Instruction.ShouldBeNull(); // generic instruction handled by the primitive
    }

    [Fact]
    public async Task ExtractAsync_NullContent_StringOverload_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() => _sut.ExtractAsync((string)null!, TestContext.Current.CancellationToken));

    [Fact]
    public async Task ExtractAsync_NullRequest_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(() => _sut.ExtractAsync((ExtractionRequest)null!, TestContext.Current.CancellationToken));
}
