using Granit.AI;
using Granit.Privacy.AI.Internal;
using Granit.Privacy.AI.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using LlmPiiItem = Granit.Privacy.AI.Internal.LlmPiiDetector.LlmPiiItem;
using LlmPiiResponse = Granit.Privacy.AI.Internal.LlmPiiDetector.LlmPiiResponse;

namespace Granit.Privacy.AI.Tests;

public sealed class LlmPiiDetectorTests
{
    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<PrivacyAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new PrivacyAIOptions
    {
        WorkspaceName = "test-privacy",
        TimeoutSeconds = 15,
        FailMode = PiiDetectionFailMode.Closed,
    });

    private LlmPiiDetector CreateDetector(PrivacyAIOptions? opts = null) =>
        new(_structured, opts is null ? _options : Microsoft.Extensions.Options.Options.Create(opts),
            NullLogger<LlmPiiDetector>.Instance);

    private void CompletionReturns(StructuredCompletionStatus status, LlmPiiResponse? value = null) =>
        _structured
            .CompleteAsync<LlmPiiResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmPiiResponse> { Status = status, Value = value });

    private void Succeeds(LlmPiiResponse value) => CompletionReturns(StructuredCompletionStatus.Succeeded, value);

    [Fact]
    public async Task ScanAsync_DetectsEmail()
    {
        Succeeds(new LlmPiiResponse(true, [new LlmPiiItem("Email", "Found email address in the text")]));

        PiiDetectionResult result = await CreateDetector().ScanAsync(
            "Party me at john@example.com for details.", TestContext.Current.CancellationToken);

        result.ContainsPii.ShouldBeTrue();
        result.Items.ShouldHaveSingleItem();
        result.Items[0].Type.ShouldBe(PiiType.Email);
        result.Items[0].Description.ShouldBe("Found email address in the text");
    }

    [Fact]
    public async Task ScanAsync_DetectsMultiplePiiTypes()
    {
        Succeeds(new LlmPiiResponse(true,
        [
            new LlmPiiItem("PersonName", "Found person name"),
            new LlmPiiItem("PhoneNumber", "Found phone number"),
            new LlmPiiItem("NationalId", "Found national ID number"),
        ]));

        PiiDetectionResult result = await CreateDetector().ScanAsync(
            "John Doe, phone 555-0123, SSN ...", TestContext.Current.CancellationToken);

        result.ContainsPii.ShouldBeTrue();
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(i => i.Type == PiiType.PersonName);
        result.Items.ShouldContain(i => i.Type == PiiType.PhoneNumber);
        result.Items.ShouldContain(i => i.Type == PiiType.NationalId);
    }

    [Fact]
    public async Task ScanAsync_NoPii_ReturnsClean()
    {
        Succeeds(new LlmPiiResponse(false, []));

        PiiDetectionResult result = await CreateDetector().ScanAsync(
            "The quarterly revenue report shows a 15% increase.", TestContext.Current.CancellationToken);

        result.ContainsPii.ShouldBeFalse();
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScanAsync_UnknownPiiType_MapsToOther()
    {
        Succeeds(new LlmPiiResponse(true, [new LlmPiiItem("Biometric", "Found biometric data")]));

        PiiDetectionResult result = await CreateDetector().ScanAsync(
            "Fingerprint data stored.", TestContext.Current.CancellationToken);

        result.Items.ShouldHaveSingleItem();
        result.Items[0].Type.ShouldBe(PiiType.Other);
        result.Items[0].Description.ShouldBe("Found biometric data");
    }

    [Fact]
    public async Task ScanAsync_RedactsPiiEchoedIntoDescription()
    {
        // The model may echo actual PII into descriptions despite the instruction; the
        // detector must redact it post-LLM.
        Succeeds(new LlmPiiResponse(true, [new LlmPiiItem("Email", "Found email john.doe@example.com in sentence 1")]));

        PiiDetectionResult result = await CreateDetector().ScanAsync("x", TestContext.Current.CancellationToken);

        result.Items[0].Description.ShouldContain("[REDACTED]");
        result.Items[0].Description.ShouldNotContain("john.doe@example.com");
    }

    [Fact]
    public async Task ScanAsync_TransportFailure_FailClosed_AssumesPiiPresent()
    {
        CompletionReturns(StructuredCompletionStatus.TransportFailure);

        PiiDetectionResult result = await CreateDetector().ScanAsync("Some text.", TestContext.Current.CancellationToken);

        result.ContainsPii.ShouldBeTrue(); // fail-closed default
        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ScanAsync_SchemaViolation_FailClosed_AssumesPiiPresent()
    {
        CompletionReturns(StructuredCompletionStatus.SchemaViolation);

        PiiDetectionResult result = await CreateDetector().ScanAsync("Some text.", TestContext.Current.CancellationToken);

        result.ContainsPii.ShouldBeTrue();
    }

    [Fact]
    public async Task ScanAsync_FailOpenConfig_AssumesNoPiiOnFailure()
    {
        CompletionReturns(StructuredCompletionStatus.TransportFailure);

        PiiDetectionResult result = await CreateDetector(new PrivacyAIOptions
        {
            WorkspaceName = "test-privacy",
            TimeoutSeconds = 15,
            FailMode = PiiDetectionFailMode.Open,
        }).ScanAsync("Some text.", TestContext.Current.CancellationToken);

        result.ContainsPii.ShouldBeFalse();
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScanAsync_PassesContentAndConfiguredWorkspace()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<LlmPiiResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<LlmPiiResponse>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new LlmPiiResponse(false, []),
            });

        await CreateDetector().ScanAsync("scan me", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.ShouldBe("scan me");
        captured.WorkspaceName.ShouldBe("test-privacy");
        captured.Instruction!.ShouldContain("PII");
    }

    [Fact]
    public async Task ScanAsync_NullText_ThrowsArgumentNullException() =>
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateDetector().ScanAsync(null!, TestContext.Current.CancellationToken));
}
