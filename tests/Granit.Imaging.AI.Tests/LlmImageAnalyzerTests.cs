using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.Imaging.AI.Diagnostics;
using Granit.Imaging.AI.Internal;
using Granit.Imaging.AI.Options;
using Granit.MultiTenancy;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.Imaging.AI.Tests;

public sealed class LlmImageAnalyzerTests
{
    private readonly IStructuredCompletion _structuredCompletion = Substitute.For<IStructuredCompletion>();
    private readonly ImagingAIMetrics _metrics = CreateTestMetrics();
    private readonly IOptions<ImagingAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new ImagingAIOptions
    {
        WorkspaceName = "vision",
        TimeoutSeconds = 30,
    });

    private static ImagingAIMetrics CreateTestMetrics()
    {
        IMeterFactory factory = Substitute.For<IMeterFactory>();
        factory.Create(Arg.Any<MeterOptions>()).Returns(new Meter("test"));
        return new ImagingAIMetrics(factory);
    }

    private static readonly ReadOnlyMemory<byte> TestImage = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

    private LlmImageAnalyzer CreateAnalyzer(ImagingAIOptions? options = null) =>
        new(_structuredCompletion,
            options is null ? _options : Microsoft.Extensions.Options.Options.Create(options),
            _metrics,
            NullTenantContext.Instance,
            NullLogger<LlmImageAnalyzer>.Instance);

    private void SetupCompletion(StructuredCompletionResult<LlmAnalysisResponse> result) =>
        _structuredCompletion
            .CompleteAsync<LlmAnalysisResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

    private static StructuredCompletionResult<LlmAnalysisResponse> Succeeded(LlmAnalysisResponse value) =>
        new() { Status = StructuredCompletionStatus.Succeeded, Value = value };

    [Fact]
    public async Task AnalyzeAsync_Succeeded_ReturnsSanitizedImageAnalysis()
    {
        SetupCompletion(Succeeded(new LlmAnalysisResponse(
            "A red car parked on a street",
            ["car", "street", "building"],
            ["outdoor", "urban", "daytime"],
            "Red car parked on urban street")));

        ImageAnalysis result = await CreateAnalyzer()
            .AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken);

        result.Description.ShouldBe("A red car parked on a street");
        result.DetectedObjects.ShouldBe(["car", "street", "building"]);
        result.Tags.ShouldBe(["outdoor", "urban", "daytime"]);
        result.SuggestedAltText.ShouldBe("Red car parked on urban street");
    }

    [Fact]
    public async Task AnalyzeAsync_SendsImageAsAttachmentWithConfiguredWorkspace()
    {
        StructuredCompletionRequest? captured = null;
        _structuredCompletion
            .CompleteAsync<LlmAnalysisResponse>(
                Arg.Do<StructuredCompletionRequest>(r => captured = r),
                Arg.Any<CancellationToken>())
            .Returns(Succeeded(new LlmAnalysisResponse("d", [], [], null)));

        await CreateAnalyzer().AnalyzeAsync(TestImage, "image/webp", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.WorkspaceName.ShouldBe("vision");
        captured.Instruction.ShouldNotBeNullOrWhiteSpace();
        // Schema pinning is the primitive's job — the instruction must not hand-roll a JSON shape.
        captured.Instruction.ShouldNotContain("JSON");
        DataContent attachment = captured.Attachments.ShouldHaveSingleItem();
        attachment.MediaType.ShouldBe("image/webp");
        attachment.Data.Span.SequenceEqual(TestImage.Span).ShouldBeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_SanitizesModelOutput()
    {
        SetupCompletion(Succeeded(new LlmAnalysisResponse(
            "desc\u0001with\u0002control chars",
            ["ok", "\u0003", new string('x', 500)],
            null,
            new string('y', 900))));

        ImageAnalysis result = await CreateAnalyzer()
            .AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken);

        result.Description.ShouldBe("descwithcontrol chars");
        result.DetectedObjects.Count.ShouldBe(2); // control-char-only entry dropped
        result.DetectedObjects[1].Length.ShouldBe(200); // item length cap
        result.Tags.ShouldBeEmpty();
        result.SuggestedAltText!.Length.ShouldBe(500); // alt text cap
    }

    [Fact]
    public async Task AnalyzeAsync_ModelRefused_ThrowsInvalidOperation()
    {
        SetupCompletion(new StructuredCompletionResult<LlmAnalysisResponse>
        {
            Status = StructuredCompletionStatus.ModelRefused,
        });

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => CreateAnalyzer().AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("The AI model returned an empty response.");
    }

    [Fact]
    public async Task AnalyzeAsync_SchemaViolation_ThrowsInvalidOperation()
    {
        SetupCompletion(new StructuredCompletionResult<LlmAnalysisResponse>
        {
            Status = StructuredCompletionStatus.SchemaViolation,
        });

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => CreateAnalyzer().AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("Failed to parse the AI model response as JSON.");
    }

    [Fact]
    public async Task AnalyzeAsync_TransportFailure_ThrowsWithPiiSafeMessage()
    {
        SetupCompletion(new StructuredCompletionResult<LlmAnalysisResponse>
        {
            Status = StructuredCompletionStatus.TransportFailure,
            ErrorMessage = "The AI request timed out after 30 seconds.",
        });

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => CreateAnalyzer().AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken));

        ex.Message.ShouldBe("The AI request timed out after 30 seconds.");
    }

    [Fact]
    public async Task AnalyzeAsync_OversizedImage_FailsFastWithoutCallingThePrimitive()
    {
        LlmImageAnalyzer analyzer = CreateAnalyzer(new ImagingAIOptions
        {
            WorkspaceName = "vision",
            TimeoutSeconds = 30,
            MaxImageBytes = 2,
        });

        await Should.ThrowAsync<ArgumentException>(
            () => analyzer.AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken));

        await _structuredCompletion.DidNotReceive().CompleteAsync<LlmAnalysisResponse>(
            Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeAsync_ZeroMaxImageBytes_DisablesTheGuard()
    {
        SetupCompletion(Succeeded(new LlmAnalysisResponse("d", [], [], null)));
        LlmImageAnalyzer analyzer = CreateAnalyzer(new ImagingAIOptions
        {
            WorkspaceName = "vision",
            TimeoutSeconds = 30,
            MaxImageBytes = 0,
        });

        ImageAnalysis result = await analyzer.AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task AnalyzeAsync_NullContentType_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateAnalyzer().AnalyzeAsync(TestImage, null!, TestContext.Current.CancellationToken));
}
