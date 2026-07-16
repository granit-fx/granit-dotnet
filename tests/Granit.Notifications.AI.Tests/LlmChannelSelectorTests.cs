using System.Text.Json;
using Granit.AI;
using Granit.Notifications.AI.Internal;
using Granit.Notifications.AI.Options;
using Granit.Notifications.AI.Schema;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.Notifications.AI.Tests;

public sealed class LlmChannelSelectorTests
{
    private static readonly IReadOnlyList<string> DefaultChannels = ["email", "push", "sms", "inapp"];

    private readonly IStructuredCompletion _structured = Substitute.For<IStructuredCompletion>();
    private readonly IOptions<NotificationsAIOptions> _options = MsOptions.Create(new NotificationsAIOptions());
    private readonly ILogger<LlmChannelSelector> _logger = NullLogger<LlmChannelSelector>.Instance;

    private LlmChannelSelector CreateSut() => new(_structured, _options, _logger);

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

    private static StructuredCompletionResult<ChannelSelectionResponse> Ok(params string[] channels) =>
        new()
        {
            Status = StructuredCompletionStatus.Succeeded,
            Value = new ChannelSelectionResponse { Channels = [.. channels] },
        };

    private void Respond(StructuredCompletionResult<ChannelSelectionResponse> result) =>
        _structured
            .CompleteAsync<ChannelSelectionResponse>(Arg.Any<StructuredCompletionRequest>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task SelectChannelsAsync_ValidResponse_ReturnsSelectedChannels()
    {
        Respond(Ok("email", "push"));

        IReadOnlyList<string> result = await CreateSut().SelectChannelsAsync(
            MakeContext(), DefaultChannels, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain("email");
        result.ShouldContain("push");
    }

    [Fact]
    public async Task SelectChannelsAsync_FiltersUnavailableChannels()
    {
        // Model invents "telegram" — dropped because it is not in the available set.
        Respond(Ok("email", "telegram", "push"));

        IReadOnlyList<string> result = await CreateSut().SelectChannelsAsync(
            MakeContext(), DefaultChannels, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result.ShouldContain("email");
        result.ShouldContain("push");
        result.ShouldNotContain("telegram");
    }

    [Theory]
    [InlineData(StructuredCompletionStatus.ModelRefused)]
    [InlineData(StructuredCompletionStatus.SchemaViolation)]
    [InlineData(StructuredCompletionStatus.TransportFailure)]
    public async Task SelectChannelsAsync_NonSuccessStatus_FallsBackToAllAvailable(StructuredCompletionStatus status)
    {
        Respond(new StructuredCompletionResult<ChannelSelectionResponse> { Status = status });

        IReadOnlyList<string> result = await CreateSut().SelectChannelsAsync(
            MakeContext(), DefaultChannels, TestContext.Current.CancellationToken);

        result.ShouldBe(DefaultChannels);
    }

    [Fact]
    public async Task SelectChannelsAsync_EmptySelectionAfterFiltering_FallsBackToAllAvailable()
    {
        // Everything the model returned is out-of-set, so the default-to-all guard kicks in.
        Respond(Ok("telegram", "whatsapp"));

        IReadOnlyList<string> result = await CreateSut().SelectChannelsAsync(
            MakeContext(), DefaultChannels, TestContext.Current.CancellationToken);

        result.ShouldBe(DefaultChannels);
    }

    [Fact]
    public async Task SelectChannelsAsync_EmptyAvailableChannels_ReturnsEmptyWithoutCallingTheModel()
    {
        IReadOnlyList<string> result = await CreateSut().SelectChannelsAsync(
            MakeContext(), [], TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
        await _structured
            .DidNotReceiveWithAnyArgs()
            .CompleteAsync<ChannelSelectionResponse>(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SelectChannelsAsync_RoutesAvailableChannelsAsInstructionAndContextAsContent()
    {
        StructuredCompletionRequest? captured = null;
        _structured
            .CompleteAsync<ChannelSelectionResponse>(Arg.Do<StructuredCompletionRequest>(r => captured = r), Arg.Any<CancellationToken>())
            .Returns(Ok("email"));

        await CreateSut().SelectChannelsAsync(
            MakeContext(NotificationSeverity.Fatal), DefaultChannels, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        // Available channels are developer/framework-controlled guidance.
        captured.Instruction.ShouldNotBeNull();
        captured.Instruction.ShouldContain("email");
        captured.Instruction.ShouldContain("push");
        // The notification context is the analyzed content.
        captured.Content.ShouldNotBeNull();
        captured.Content.ShouldContain("order.completed");
        captured.Content.ShouldContain("Fatal");
    }
}
