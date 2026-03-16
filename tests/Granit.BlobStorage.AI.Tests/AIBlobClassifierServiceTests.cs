using Granit.AI;
using Granit.BlobStorage.AI.Internal;
using Granit.BlobStorage.AI.Options;
using Granit.BlobStorage.Domain;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.BlobStorage.AI.Tests;

public sealed class AIBlobClassifierServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 16, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TestTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<BlobStorageAIOptions> _options = MsOptions.Create(new BlobStorageAIOptions());
    private readonly ILogger<AIBlobClassifierService> _logger = NullLogger<AIBlobClassifierService>.Instance;

    public AIBlobClassifierServiceTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);
    }

    private AIBlobClassifierService CreateSut() => new(_chatClientFactory, _options, _logger);

    private static BlobDescriptor MakeDescriptor(string fileName, string contentType) =>
        BlobDescriptor.Create(
            id: Guid.NewGuid(),
            tenantId: TestTenantId,
            containerName: "uploads",
            objectKey: $"{TestTenantId}/uploads/2026/03/some-id",
            request: new BlobUploadRequest(fileName, contentType, 10_000_000L),
            createdAt: Now);

    private static BlobValidationContext MakeContext(string fileName, string contentType) =>
        new()
        {
            Descriptor = MakeDescriptor(fileName, contentType),
            ActualSizeBytes = 1024,
            OpenPartialStreamAsync = (_, _) =>
                Task.FromResult<Stream>(new MemoryStream(Array.Empty<byte>())),
        };

    [Fact]
    public async Task ClassifyAsync_ValidResponse_ReturnsClassification()
    {
        AIBlobClassifierService sut = CreateSut();
        string json = """{"category": "invoice", "confidence": 0.95, "tags": ["financial", "document"], "containsPiiInFileName": false}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, json)]));

        BlobClassification result = await sut.ClassifyAsync(
            "invoice-2026-03.pdf", "application/pdf", TestContext.Current.CancellationToken);

        result.Category.ShouldBe("invoice");
        result.Confidence.ShouldBe(0.95);
        result.DetectedTags.ShouldContain("financial");
        result.DetectedTags.ShouldContain("document");
        result.ContainsPiiInFileName.ShouldBeFalse();
    }

    [Fact]
    public async Task ClassifyAsync_DetectsPiiInFileName()
    {
        AIBlobClassifierService sut = CreateSut();
        string json = """{"category": "identity_document", "confidence": 0.88, "tags": ["personal"], "containsPiiInFileName": true}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, json)]));

        BlobClassification result = await sut.ClassifyAsync(
            "john-doe-ssn-123456789.pdf", "application/pdf", TestContext.Current.CancellationToken);

        result.ContainsPiiInFileName.ShouldBeTrue();
        result.Category.ShouldBe("identity_document");
    }

    [Fact]
    public async Task ClassifyAsync_LLMFailure_ReturnsUnknown()
    {
        AIBlobClassifierService sut = CreateSut();

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        BlobClassification result = await sut.ClassifyAsync(
            "report.pdf", "application/pdf", TestContext.Current.CancellationToken);

        result.Category.ShouldBe("unknown");
        result.Confidence.ShouldBe(0.0);
        result.DetectedTags.ShouldBeEmpty();
        result.ContainsPiiInFileName.ShouldBeFalse();
    }

    [Fact]
    public async Task ValidateAsync_ReturnsValidResult()
    {
        AIBlobClassifierService sut = CreateSut();
        string json = """{"category": "photo", "confidence": 0.92, "tags": ["image"], "containsPiiInFileName": false}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, json)]));

        BlobValidationContext context = MakeContext("vacation-photo.jpg", "image/jpeg");

        BlobValidationResult result = await sut.ValidateAsync(
            context, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_PiiDetected_ReturnsFailure()
    {
        AIBlobClassifierService sut = CreateSut();
        string json = """{"category": "identity_document", "confidence": 0.9, "tags": ["personal"], "containsPiiInFileName": true}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, json)]));

        BlobValidationContext context = MakeContext("john-doe-ssn-123456789.pdf", "application/pdf");

        BlobValidationResult result = await sut.ValidateAsync(
            context, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNull();
        result.FailureReason.ShouldContain("personally identifiable information");
    }

    [Fact]
    public async Task ValidateAsync_PiiDetected_ButDisabled_ReturnsValid()
    {
        IOptions<BlobStorageAIOptions> disabledPiiOptions = MsOptions.Create(
            new BlobStorageAIOptions { EnablePiiDetection = false });
        var sut = new AIBlobClassifierService(_chatClientFactory, disabledPiiOptions, _logger);

        string json = """{"category": "identity_document", "confidence": 0.9, "tags": ["personal"], "containsPiiInFileName": true}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, json)]));

        BlobValidationContext context = MakeContext("john-doe-ssn-123456789.pdf", "application/pdf");

        BlobValidationResult result = await sut.ValidateAsync(
            context, TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Order_Is100() =>
        CreateSut().Order.ShouldBe(100);

    [Fact]
    public void ParseClassificationResponse_ValidJson_ReturnsClassification()
    {
        string json = """{"category": "contract", "confidence": 0.85, "tags": ["legal"], "containsPiiInFileName": false}""";

        BlobClassification result = AIBlobClassifierService.ParseClassificationResponse(json);

        result.Category.ShouldBe("contract");
        result.Confidence.ShouldBe(0.85);
        result.DetectedTags.ShouldContain("legal");
    }

    [Fact]
    public void ParseClassificationResponse_MarkdownFencedJson_ReturnsClassification()
    {
        string json = """
            ```json
            {"category": "invoice", "confidence": 0.9, "tags": [], "containsPiiInFileName": false}
            ```
            """;

        BlobClassification result = AIBlobClassifierService.ParseClassificationResponse(json);

        result.Category.ShouldBe("invoice");
    }

    [Fact]
    public void ParseClassificationResponse_InvalidJson_ReturnsUnknown()
    {
        BlobClassification result = AIBlobClassifierService.ParseClassificationResponse("not valid json");

        result.Category.ShouldBe("unknown");
        result.Confidence.ShouldBe(0.0);
    }

    [Fact]
    public void ParseClassificationResponse_ClampsConfidenceAbove1()
    {
        string json = """{"category": "photo", "confidence": 1.5, "tags": [], "containsPiiInFileName": false}""";

        BlobClassification result = AIBlobClassifierService.ParseClassificationResponse(json);

        result.Confidence.ShouldBe(1.0);
    }

    [Fact]
    public void BuildClassificationPrompt_ContainsFileNameAndContentType()
    {
        string prompt = AIBlobClassifierService.BuildClassificationPrompt("test.pdf", "application/pdf");

        prompt.ShouldContain("test.pdf");
        prompt.ShouldContain("application/pdf");
    }
}
