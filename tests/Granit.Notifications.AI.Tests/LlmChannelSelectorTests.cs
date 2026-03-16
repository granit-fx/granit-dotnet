using System.Text.Json;
using Granit.AI;
using Granit.Notifications.AI.Internal;
using Granit.Notifications.AI.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Notifications.AI.Tests;

public sealed class LlmChannelSelectorTests
{
    private static readonly IReadOnlyList<string> DefaultChannels = ["email", "push", "sms", "inapp"];

    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<NotificationsAIOptions> _options = MsOptions.Create(new NotificationsAIOptions());
    private readonly ILogger<LlmChannelSelector> _logger = NullLogger<LlmChannelSelector>.Instance;

    public LlmChannelSelectorTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);
    }

    private LlmChannelSelector CreateSut() => new(_chatClientFactory, _options, _logger);

    private static NotificationDeliveryContext MakeContext(
        NotificationSeverity severity = NotificationSeverity.Info) =>
        new()
        {
            NotificationId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationTypeName = "order.completed",
            Severity = severity,
            RecipientUserId = "user-123",
            Data = JsonSerializer.SerializeToElement(new { OrderId = "ORD-001" }),
            OccurredAt = new DateTimeOffset(2026, 3, 16, 14, 0, 0, TimeSpan.Zero),
            Culture = "en",
        };

    [Fact]
    public async Task SelectChannelsAsync_ValidResponse_ReturnsSelectedChannels()
    {
        LlmChannelSelector sut = CreateSut();
        string json = """["email", "push"]""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, json)]));

        IReadOnlyList<string> result = await sut.SelectChannelsAsync(
            MakeContext(), DefaultChannels, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain("email");
        result.ShouldContain("push");
    }

    [Fact]
    public async Task SelectChannelsAsync_LLMFailure_ReturnsAllAvailable()
    {
        LlmChannelSelector sut = CreateSut();

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        IReadOnlyList<string> result = await sut.SelectChannelsAsync(
            MakeContext(), DefaultChannels, TestContext.Current.CancellationToken);

        result.ShouldBe(DefaultChannels);
    }

    [Fact]
    public async Task SelectChannelsAsync_EmptyAvailableChannels_ReturnsEmpty()
    {
        LlmChannelSelector sut = CreateSut();

        IReadOnlyList<string> result = await sut.SelectChannelsAsync(
            MakeContext(), [], TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ParseChannelSelectionResponse_ValidJson_ReturnsChannels()
    {
        string json = """["email", "sms"]""";

        IReadOnlyList<string> result = LlmChannelSelector.ParseChannelSelectionResponse(json, DefaultChannels);

        result.Count.ShouldBe(2);
        result.ShouldContain("email");
        result.ShouldContain("sms");
    }

    [Fact]
    public void ParseChannelSelectionResponse_FiltersUnavailableChannels()
    {
        string json = """["email", "telegram", "push"]""";

        IReadOnlyList<string> result = LlmChannelSelector.ParseChannelSelectionResponse(json, DefaultChannels);

        result.Count.ShouldBe(2);
        result.ShouldContain("email");
        result.ShouldContain("push");
        result.ShouldNotContain("telegram");
    }

    [Fact]
    public void ParseChannelSelectionResponse_MarkdownFencedJson_ReturnsChannels()
    {
        string json = """
            ```json
            ["email", "push"]
            ```
            """;

        IReadOnlyList<string> result = LlmChannelSelector.ParseChannelSelectionResponse(json, DefaultChannels);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public void ParseChannelSelectionResponse_InvalidJson_ReturnsEmpty()
    {
        IReadOnlyList<string> result = LlmChannelSelector.ParseChannelSelectionResponse("not json", DefaultChannels);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void BuildChannelSelectionPrompt_ContainsSeverityAndChannels()
    {
        NotificationDeliveryContext context = MakeContext(NotificationSeverity.Fatal);

        string prompt = LlmChannelSelector.BuildChannelSelectionPrompt(context, DefaultChannels);

        prompt.ShouldContain("Fatal");
        prompt.ShouldContain("email");
        prompt.ShouldContain("push");
        prompt.ShouldContain("order.completed");
    }
}
