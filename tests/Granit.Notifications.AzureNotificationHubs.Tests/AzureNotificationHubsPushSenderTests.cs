// =============================================================================
// Tests - AzureNotificationHubsPushSender
// =============================================================================
// Verifies the Azure Notification Hubs push sender: message mapping, transport
// delegation, activity tracing, argument validation, and logging.
// Uses IAzureNotificationHubsTransport substitute to avoid real Azure calls.
// =============================================================================

using System.Text.Json;
using Granit.Notifications.AzureNotificationHubs.Internal;
using Granit.Notifications.MobilePush;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.AzureNotificationHubs.Tests;

public sealed class AzureNotificationHubsPushSenderTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static (AzureNotificationHubsPushSender Sender, IAzureNotificationHubsTransport Transport)
        CreateSender(ILogger<AzureNotificationHubsPushSender>? logger = null)
    {
        IAzureNotificationHubsTransport transport = Substitute.For<IAzureNotificationHubsTransport>();
        AzureNotificationHubsPushSender sender = new(
            transport,
            logger ?? NullLogger<AzureNotificationHubsPushSender>.Instance);
        return (sender, transport);
    }

    private static MobilePushMessage SimpleMessage(
        IReadOnlyList<string>? tokens = null,
        JsonElement? data = null) =>
        new()
        {
            DeviceTokens = tokens ?? ["token-1", "token-2"],
            Title = "Test Title",
            Body = "Test body text",
            Data = data,
        };

    // -------------------------------------------------------------------------
    // Class structure
    // -------------------------------------------------------------------------

    [Fact]
    public void Class_Implements_IMobilePushSender()
    {
        (AzureNotificationHubsPushSender sender, _) = CreateSender();
        sender.ShouldBeAssignableTo<IMobilePushSender>();
    }

    [Fact]
    public void Class_IsSealed() =>
        typeof(AzureNotificationHubsPushSender).IsSealed.ShouldBeTrue();

    [Fact]
    public void Class_IsInternal() =>
        typeof(AzureNotificationHubsPushSender).IsNotPublic.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // SendAsync — transport delegation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_DelegatesToTransport()
    {
        (AzureNotificationHubsPushSender sender, IAzureNotificationHubsTransport transport) = CreateSender();

        await sender.SendAsync(SimpleMessage(), TestContext.Current.CancellationToken);

        await transport.Received(1).SendAsync(
            Arg.Any<NotificationHubsMessage>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_MapsMessageFieldsToTransport()
    {
        (AzureNotificationHubsPushSender sender, IAzureNotificationHubsTransport transport) = CreateSender();
        NotificationHubsMessage? captured = null;
        await transport.SendAsync(
            Arg.Do<NotificationHubsMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        List<string> tokens = ["device-a", "device-b", "device-c"];
        await sender.SendAsync(new MobilePushMessage
        {
            DeviceTokens = tokens,
            Title = "Alert",
            Body = "Something happened",
        }, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Title.ShouldBe("Alert");
        captured.Body.ShouldBe("Something happened");
        captured.DeviceTokens.ShouldBe(tokens);
        captured.Data.ShouldBeNull();
    }

    [Fact]
    public async Task SendAsync_WithData_PassesDataToTransport()
    {
        (AzureNotificationHubsPushSender sender, IAzureNotificationHubsTransport transport) = CreateSender();
        NotificationHubsMessage? captured = null;
        await transport.SendAsync(
            Arg.Do<NotificationHubsMessage>(m => captured = m),
            Arg.Any<CancellationToken>());

        JsonElement data = JsonDocument.Parse("""{"orderId":"123"}""").RootElement;
        await sender.SendAsync(SimpleMessage(data: data), TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Data.ShouldNotBeNull();
        captured.Data.Value.GetProperty("orderId").GetString().ShouldBe("123");
    }

    [Fact]
    public async Task SendAsync_PassesCancellationToken()
    {
        (AzureNotificationHubsPushSender sender, IAzureNotificationHubsTransport transport) = CreateSender();
        using CancellationTokenSource cts = new();
        CancellationToken token = cts.Token;

        await sender.SendAsync(SimpleMessage(), token);

        await transport.Received(1).SendAsync(
            Arg.Any<NotificationHubsMessage>(),
            token);
    }

    // -------------------------------------------------------------------------
    // Argument validation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_NullMessage_ThrowsArgumentNullException()
    {
        (AzureNotificationHubsPushSender sender, _) = CreateSender();

        await Should.ThrowAsync<ArgumentNullException>(
            () => sender.SendAsync(null!, TestContext.Current.CancellationToken));
    }

    // -------------------------------------------------------------------------
    // Logging
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendAsync_LogsPushSent()
    {
        ILogger<AzureNotificationHubsPushSender> logger =
            Substitute.For<ILogger<AzureNotificationHubsPushSender>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        (AzureNotificationHubsPushSender sender, _) = CreateSender(logger);

        await sender.SendAsync(SimpleMessage(tokens: ["t1", "t2", "t3"]),
            TestContext.Current.CancellationToken);

        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains('3')),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
