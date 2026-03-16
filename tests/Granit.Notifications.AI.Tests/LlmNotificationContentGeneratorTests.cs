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

public sealed class LlmNotificationContentGeneratorTests
{
    private readonly IAIChatClientFactory _chatClientFactory = Substitute.For<IAIChatClientFactory>();
    private readonly IChatClient _chatClient = Substitute.For<IChatClient>();
    private readonly IOptions<NotificationsAIOptions> _options = MsOptions.Create(new NotificationsAIOptions());
    private readonly ILogger<LlmNotificationContentGenerator> _logger = NullLogger<LlmNotificationContentGenerator>.Instance;

    public LlmNotificationContentGeneratorTests()
    {
        _chatClientFactory
            .CreateAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_chatClient);
    }

    private LlmNotificationContentGenerator CreateSut() => new(_chatClientFactory, _options, _logger);

    private static NotificationDeliveryContext MakeContext(
        string typeName = "order.completed",
        NotificationSeverity severity = NotificationSeverity.Info,
        string? culture = "en") =>
        new()
        {
            NotificationId = Guid.NewGuid(),
            DeliveryId = Guid.NewGuid(),
            NotificationTypeName = typeName,
            Severity = severity,
            RecipientUserId = "user-123",
            Data = JsonSerializer.SerializeToElement(new { OrderId = "ORD-001", Total = 99.99 }),
            OccurredAt = new DateTimeOffset(2026, 3, 16, 14, 0, 0, TimeSpan.Zero),
            Culture = culture,
        };

    [Fact]
    public async Task GenerateAsync_ValidResponse_ReturnsContent()
    {
        LlmNotificationContentGenerator sut = CreateSut();
        string json = """{"subject": "Order Completed", "body": "Your order ORD-001 has been completed."}""";

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, json)]));

        NotificationContent? result = await sut.GenerateAsync(
            MakeContext(), TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Subject.ShouldBe("Order Completed");
        result.Body.ShouldBe("Your order ORD-001 has been completed.");
    }

    [Fact]
    public async Task GenerateAsync_LLMFailure_ReturnsNull()
    {
        LlmNotificationContentGenerator sut = CreateSut();

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("LLM unavailable"));

        NotificationContent? result = await sut.GenerateAsync(
            MakeContext(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateAsync_InvalidJsonResponse_ReturnsNull()
    {
        LlmNotificationContentGenerator sut = CreateSut();

        _chatClient
            .GetResponseAsync(
                Arg.Any<IEnumerable<ChatMessage>>(),
                Arg.Any<ChatOptions?>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatResponse(
                [new ChatMessage(ChatRole.Assistant, "not valid json")]));

        NotificationContent? result = await sut.GenerateAsync(
            MakeContext(), TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseContentResponse_ValidJson_ReturnsContent()
    {
        string json = """{"subject": "Alert", "body": "Something happened."}""";

        NotificationContent? result = LlmNotificationContentGenerator.ParseContentResponse(json);

        result.ShouldNotBeNull();
        result.Subject.ShouldBe("Alert");
        result.Body.ShouldBe("Something happened.");
    }

    [Fact]
    public void ParseContentResponse_MarkdownFencedJson_ReturnsContent()
    {
        string json = """
            ```json
            {"subject": "Alert", "body": "Something happened."}
            ```
            """;

        NotificationContent? result = LlmNotificationContentGenerator.ParseContentResponse(json);

        result.ShouldNotBeNull();
        result.Subject.ShouldBe("Alert");
    }

    [Fact]
    public void ParseContentResponse_MissingSubject_ReturnsNull()
    {
        string json = """{"subject": "", "body": "Some body"}""";

        NotificationContent? result = LlmNotificationContentGenerator.ParseContentResponse(json);

        result.ShouldBeNull();
    }

    [Fact]
    public void ParseContentResponse_InvalidJson_ReturnsNull()
    {
        NotificationContent? result = LlmNotificationContentGenerator.ParseContentResponse("not json");

        result.ShouldBeNull();
    }

    [Fact]
    public void BuildContentPrompt_ContainsNotificationTypeAndCulture()
    {
        NotificationDeliveryContext context = MakeContext(typeName: "user.registered", culture: "fr");

        string prompt = LlmNotificationContentGenerator.BuildContentPrompt(context);

        prompt.ShouldContain("user.registered");
        prompt.ShouldContain("fr");
    }
}
