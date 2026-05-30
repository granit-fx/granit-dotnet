using Granit.AI;
using Granit.BlobStorage.AI.Internal;
using Granit.BlobStorage.AI.Options;
using Granit.BlobStorage.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;
using ClassificationJson = Granit.BlobStorage.AI.Internal.AIBlobClassifierService.ClassificationJson;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.BlobStorage.AI.Tests;

public sealed class AIBlobClassifierServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<BlobStorageAIOptions> _options = MsOptions.Create(new BlobStorageAIOptions());

    private AIBlobClassifierService CreateSut(IOptions<BlobStorageAIOptions>? opts = null) =>
        new(_structured, opts ?? _options, NullLogger<AIBlobClassifierService>.Instance);

    private void Succeeds(ClassificationJson value) =>
        _structured
            .CompleteAsync<ClassificationJson>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<ClassificationJson> { Status = StructuredCompletionStatus.Succeeded, Value = value });

    private void Fails(StructuredCompletionStatus status) =>
        _structured
            .CompleteAsync<ClassificationJson>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<ClassificationJson> { Status = status });

    private static BlobValidationContext MakeContext(string fileName, string contentType) =>
        new()
        {
            Descriptor = BlobDescriptor.Create(
                id: Guid.NewGuid(),
                tenantId: TestTenantId,
                containerName: "uploads",
                objectKey: $"{TestTenantId}/uploads/2026/03/some-id",
                request: new BlobUploadRequest(fileName, contentType, 10_000_000L),
                createdAt: Now),
            ActualSizeBytes = 1024,
            OpenPartialStreamAsync = (_, _) => Task.FromResult<Stream>(new MemoryStream([])),
        };

    [Fact]
    public async Task ClassifyAsync_ValidResponse_ReturnsClassification()
    {
        Succeeds(new ClassificationJson("invoice", 0.95, ["financial", "document"], false));

        BlobClassification result = await CreateSut().ClassifyAsync(
            "invoice-2026-03.pdf", "application/pdf", TestContext.Current.CancellationToken);

        result.Category.ShouldBe("invoice");
        result.Confidence.ShouldBe(0.95);
        result.DetectedTags.ShouldContain("financial");
        result.ContainsPiiInFileName.ShouldBeFalse();
    }

    [Fact]
    public async Task ClassifyAsync_DetectsPiiInFileName()
    {
        Succeeds(new ClassificationJson("identity_document", 0.88, ["personal"], true));

        BlobClassification result = await CreateSut().ClassifyAsync(
            "john-doe-ssn-123456789.pdf", "application/pdf", TestContext.Current.CancellationToken);

        result.ContainsPiiInFileName.ShouldBeTrue();
        result.Category.ShouldBe("identity_document");
    }

    [Fact]
    public async Task ClassifyAsync_ClampsConfidenceAbove1()
    {
        Succeeds(new ClassificationJson("photo", 1.5, [], false));

        BlobClassification result = await CreateSut().ClassifyAsync(
            "p.jpg", "image/jpeg", TestContext.Current.CancellationToken);

        result.Confidence.ShouldBe(1.0);
    }

    [Fact]
    public async Task ClassifyAsync_NullCategoryAndTags_DefaultsApplied()
    {
        Succeeds(new ClassificationJson(null, 0.5, null, false));

        BlobClassification result = await CreateSut().ClassifyAsync(
            "x.bin", "application/octet-stream", TestContext.Current.CancellationToken);

        result.Category.ShouldBe("unknown");
        result.DetectedTags.ShouldBeEmpty();
    }

    [Fact]
    public async Task ClassifyAsync_Unavailable_ReturnsUnknown()
    {
        Fails(StructuredCompletionStatus.TransportFailure);

        BlobClassification result = await CreateSut().ClassifyAsync(
            "report.pdf", "application/pdf", TestContext.Current.CancellationToken);

        result.Category.ShouldBe("unknown");
        result.Confidence.ShouldBe(0.0);
        result.DetectedTags.ShouldBeEmpty();
        result.ContainsPiiInFileName.ShouldBeFalse();
    }

    [Fact]
    public async Task ClassifyAsync_SchemaViolation_ReturnsUnknown()
    {
        Fails(StructuredCompletionStatus.SchemaViolation);

        BlobClassification result = await CreateSut().ClassifyAsync(
            "report.pdf", "application/pdf", TestContext.Current.CancellationToken);

        result.Category.ShouldBe("unknown");
    }

    [Fact]
    public async Task ClassifyAsync_PassesFileNameAsContentAndContentTypeAsContext()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<ClassificationJson>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(new StructuredCompletionResult<ClassificationJson>
            {
                Status = StructuredCompletionStatus.Succeeded,
                Value = new ClassificationJson("other", 0.1, [], false),
            });

        await CreateSut().ClassifyAsync("test.pdf", "application/pdf", TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Content.ShouldBe("test.pdf");
        captured.Context.ShouldNotBeNull();
        captured.Context.ShouldContain(kv => kv.Key == "Content type" && kv.Value == "application/pdf");
    }

    [Fact]
    public async Task ValidateAsync_ReturnsValidResult()
    {
        Succeeds(new ClassificationJson("photo", 0.92, ["image"], false));

        BlobValidationResult result = await CreateSut().ValidateAsync(
            MakeContext("vacation-photo.jpg", "image/jpeg"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_PiiDetected_ReturnsFailure()
    {
        Succeeds(new ClassificationJson("identity_document", 0.9, ["personal"], true));

        BlobValidationResult result = await CreateSut().ValidateAsync(
            MakeContext("john-doe-ssn-123456789.pdf", "application/pdf"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull();
        result.FailureReason.ShouldContain("personally identifiable information");
    }

    [Fact]
    public async Task ValidateAsync_PiiDetected_ButDisabled_ReturnsValid()
    {
        Succeeds(new ClassificationJson("identity_document", 0.9, ["personal"], true));

        BlobValidationResult result = await CreateSut(MsOptions.Create(new BlobStorageAIOptions { EnablePiiDetection = false }))
            .ValidateAsync(MakeContext("john-doe-ssn-123456789.pdf", "application/pdf"), TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Order_Is100() => CreateSut().Order.ShouldBe(100);
}
