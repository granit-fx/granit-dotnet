using System.Diagnostics.Metrics;
using Granit.AI;
using Granit.Imaging.AI.Diagnostics;
using Granit.Imaging.AI.Internal;
using Granit.Imaging.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;

namespace Granit.Imaging.AI.Tests;

public sealed class LlmImageAnalyzerTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
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

    public LlmImageAnalyzerTests()
    {
        _chatClientFactory.CreateAsync("vision", Arg.Any<CancellationToken>()).Returns(_chatClient);
    }

    [Fact]
    public async Task AnalyzeAsync_ValidResponse_ReturnsImageAnalysis()
    {
        const string json = """
            {
                "description": "A red car parked on a street",
                "detectedObjects": ["car", "street", "building"],
                "tags": ["outdoor", "urban", "daytime"],
                "suggestedAltText": "Red car parked on urban street"
            }
            """;

        SetupChatResponse(json);

        LlmImageAnalyzer analyzer = CreateAnalyzer();

        ImageAnalysis result = await analyzer.AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken);

        result.Description.ShouldBe("A red car parked on a street");
        result.DetectedObjects.ShouldBe(["car", "street", "building"]);
        result.Tags.ShouldBe(["outdoor", "urban", "daytime"]);
        result.SuggestedAltText.ShouldBe("Red car parked on urban street");
    }

    [Fact]
    public async Task AnalyzeAsync_ResponseWithMarkdownFences_ParsesCorrectly()
    {
        const string json = """
            ```json
            {
                "description": "A cat sitting on a windowsill",
                "detectedObjects": ["cat", "window"],
                "tags": ["indoor", "pet"],
                "suggestedAltText": "Cat on windowsill"
            }
            ```
            """;

        SetupChatResponse(json);

        LlmImageAnalyzer analyzer = CreateAnalyzer();

        ImageAnalysis result = await analyzer.AnalyzeAsync(TestImage, "image/jpeg", TestContext.Current.CancellationToken);

        result.Description.ShouldBe("A cat sitting on a windowsill");
        result.DetectedObjects.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AnalyzeAsync_NullAltText_ReturnsNullSuggestedAltText()
    {
        const string json = """
            {
                "description": "Abstract art",
                "detectedObjects": [],
                "tags": ["abstract"],
                "suggestedAltText": null
            }
            """;

        SetupChatResponse(json);

        LlmImageAnalyzer analyzer = CreateAnalyzer();

        ImageAnalysis result = await analyzer.AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken);

        result.SuggestedAltText.ShouldBeNull();
        result.DetectedObjects.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_InvalidJsonResponse_ThrowsJsonException()
    {
        SetupChatResponse("not valid json");

        LlmImageAnalyzer analyzer = CreateAnalyzer();

        await Should.ThrowAsync<System.Text.Json.JsonException>(
            () => analyzer.AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnalyzeAsync_NullContentType_ThrowsArgumentNullException()
    {
        LlmImageAnalyzer analyzer = CreateAnalyzer();

        await Should.ThrowAsync<ArgumentNullException>(
            () => analyzer.AnalyzeAsync(TestImage, null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnalyzeAsync_UsesConfiguredWorkspaceName()
    {
        const string json = """
            {
                "description": "Test",
                "detectedObjects": [],
                "tags": [],
                "suggestedAltText": null
            }
            """;

        SetupChatResponse(json);

        LlmImageAnalyzer analyzer = CreateAnalyzer();

        await analyzer.AnalyzeAsync(TestImage, "image/png", TestContext.Current.CancellationToken);

        await _chatClientFactory.Received(1).CreateAsync("vision", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnalyzeAsync_SendsImageDataAsChatMessage()
    {
        const string json = """
            {
                "description": "Test image",
                "detectedObjects": [],
                "tags": [],
                "suggestedAltText": null
            }
            """;

        SetupChatResponse(json);

        LlmImageAnalyzer analyzer = CreateAnalyzer();

        await analyzer.AnalyzeAsync(TestImage, "image/webp", TestContext.Current.CancellationToken);

        await _chatClient.Received(1).GetResponseAsync(
            Arg.Is<IEnumerable<ChatMessage>>(msgs =>
                msgs.Any(m => m.Role == ChatRole.User &&
                    m.Contents.OfType<DataContent>().Any(dc => dc.MediaType == "image/webp") &&
                    m.Contents.OfType<TextContent>().Any())),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>());
    }

    private LlmImageAnalyzer CreateAnalyzer() =>
        new(_chatClientFactory, _options, _metrics, NullLogger<LlmImageAnalyzer>.Instance);

    private void SetupChatResponse(string? text)
    {
        ChatResponse response = new([new ChatMessage(ChatRole.Assistant, text)]);

        _chatClient.GetResponseAsync(
            Arg.Any<IEnumerable<ChatMessage>>(),
            Arg.Any<ChatOptions>(),
            Arg.Any<CancellationToken>()).Returns(response);
    }
}
