using Granit.AI;
using Granit.Privacy.AI.Internal;
using Granit.Privacy.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;

namespace Granit.Privacy.AI.Tests;

public sealed class LlmPiiDetectorTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<PrivacyAIOptions> _options = Microsoft.Extensions.Options.Options.Create(new PrivacyAIOptions
    {
        WorkspaceName = "test-privacy",
        TimeoutSeconds = 15,
    });

    private readonly LlmPiiDetector _sut;

    public LlmPiiDetectorTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);

        _sut = new LlmPiiDetector(
            _chatClientFactory,
            _options,
            NullLogger<LlmPiiDetector>.Instance);
    }

    [Fact]
    public async Task ScanAsync_DetectsEmail()
    {
        // Arrange
        const string jsonResponse = """{"containsPii":true,"items":[{"type":"Email","description":"Found email address in the text"}]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        PiiDetectionResult result = await _sut.ScanAsync(
            "Contact me at john@example.com for details.",
            TestContext.Current.CancellationToken);

        // Assert
        result.ContainsPii.ShouldBeTrue();
        result.Items.ShouldHaveSingleItem();
        result.Items[0].Type.ShouldBe(PiiType.Email);
        result.Items[0].Description.ShouldBe("Found email address in the text");
    }

    [Fact]
    public async Task ScanAsync_DetectsMultiplePiiTypes()
    {
        // Arrange
        const string jsonResponse = """
            {
                "containsPii": true,
                "items": [
                    {"type": "PersonName", "description": "Found person name"},
                    {"type": "PhoneNumber", "description": "Found phone number"},
                    {"type": "NationalId", "description": "Found national ID number"}
                ]
            }
            """;

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        PiiDetectionResult result = await _sut.ScanAsync(
            "John Doe, phone 555-0123, SSN 123-45-6789",
            TestContext.Current.CancellationToken);

        // Assert
        result.ContainsPii.ShouldBeTrue();
        result.Items.Count.ShouldBe(3);
        result.Items.ShouldContain(i => i.Type == PiiType.PersonName);
        result.Items.ShouldContain(i => i.Type == PiiType.PhoneNumber);
        result.Items.ShouldContain(i => i.Type == PiiType.NationalId);
    }

    [Fact]
    public async Task ScanAsync_NoPii_ReturnsClean()
    {
        // Arrange
        const string jsonResponse = """{"containsPii":false,"items":[]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        PiiDetectionResult result = await _sut.ScanAsync(
            "The quarterly revenue report shows a 15% increase.",
            TestContext.Current.CancellationToken);

        // Assert
        result.ContainsPii.ShouldBeFalse();
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ScanAsync_LLMFailure_FailClosed_AssumesPiiPresent()
    {
        // Arrange — default FailMode is Closed
        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Provider unavailable"));

        // Act
        PiiDetectionResult result = await _sut.ScanAsync(
            "Some text to scan.",
            TestContext.Current.CancellationToken);

        // Assert — fail-closed: assume PII present when detection fails
        result.ContainsPii.ShouldBeTrue();
        result.Items.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ScanAsync_NullText_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.ScanAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ScanAsync_MarkdownCodeFences_StripsAndParses()
    {
        // Arrange
        const string jsonWithFences = """
            ```json
            {"containsPii":true,"items":[{"type":"Email","description":"Found email"}]}
            ```
            """;

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonWithFences)));

        // Act
        PiiDetectionResult result = await _sut.ScanAsync(
            "Contact: test@example.com",
            TestContext.Current.CancellationToken);

        // Assert
        result.ContainsPii.ShouldBeTrue();
        result.Items.ShouldHaveSingleItem();
        result.Items[0].Type.ShouldBe(PiiType.Email);
    }

    [Fact]
    public async Task ScanAsync_UnknownPiiType_MapsToOther()
    {
        // Arrange
        const string jsonResponse = """{"containsPii":true,"items":[{"type":"Biometric","description":"Found biometric data"}]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        PiiDetectionResult result = await _sut.ScanAsync(
            "Fingerprint data stored.",
            TestContext.Current.CancellationToken);

        // Assert
        result.ContainsPii.ShouldBeTrue();
        result.Items.ShouldHaveSingleItem();
        result.Items[0].Type.ShouldBe(PiiType.Other);
        result.Items[0].Description.ShouldBe("Found biometric data");
    }

    [Fact]
    public async Task ScanAsync_UsesConfiguredWorkspace()
    {
        // Arrange
        const string jsonResponse = """{"containsPii":false,"items":[]}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        // Act
        await _sut.ScanAsync("test", TestContext.Current.CancellationToken);

        // Assert — verify the configured workspace name was used
        await _chatClientFactory.Received(1).CreateAsync("test-privacy", Arg.Any<CancellationToken>());
    }
}
