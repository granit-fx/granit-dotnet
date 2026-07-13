// =============================================================================
// Tests - SseNotificationChannel
// =============================================================================
// Verifies the SSE channel implementation: correct delegation to the connection
// manager, field mapping to SseNotificationMessage, and handling of optional
// RelatedEntity.
// =============================================================================

using System.Text.Json;
using Granit.Domain;
using Granit.Notifications.Sse.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sse.Tests;

public sealed class SseNotificationChannelTests
{
    private readonly ISseConnectionManager _connectionManager = Substitute.For<ISseConnectionManager>();
    private readonly SseNotificationChannel _channel;
    private SseNotificationMessage? _captured;

    public SseNotificationChannelTests()
    {
#pragma warning disable CA2012 // NSubstitute setup pattern
        _connectionManager.SendToUserAsync(
            Arg.Any<string>(),
            Arg.Do<SseNotificationMessage>(msg => _captured = msg),
            Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(ValueTask.CompletedTask);
#pragma warning restore CA2012

        _channel = new SseNotificationChannel(_connectionManager, NullLogger<SseNotificationChannel>.Instance);
    }

    [Fact]
    public void Name_ReturnsSse() =>
        _channel.Name.ShouldBe(NotificationChannels.Sse);

    [Fact]
    public async Task SendAsync_DelegatesToConnectionManager()
    {
        NotificationDeliveryContext context = BuildContext();

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _connectionManager.Received(1).SendToUserAsync(
            context.RecipientUserId,
            Arg.Any<SseNotificationMessage>(),
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SendAsync_MapsFieldsCorrectly()
    {
        NotificationDeliveryContext context = BuildContext();

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        _captured.ShouldNotBeNull();
        _captured!.NotificationId.ShouldBe(context.NotificationId);
        _captured.NotificationTypeName.ShouldBe(context.NotificationTypeName);
        _captured.Severity.ShouldBe(context.Severity);
        _captured.Data!.Value.GetProperty("key").GetString().ShouldBe("value");
        _captured.OccurredAt.ShouldBe(context.OccurredAt);
    }

    [Fact]
    public async Task SendAsync_MapsRelatedEntity_WhenPresent()
    {
        NotificationDeliveryContext context = BuildContext(withRelatedEntity: true);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        _captured.ShouldNotBeNull();
        _captured!.RelatedEntityType.ShouldBe("Patient");
        _captured.RelatedEntityId.ShouldBe("pat-1");
    }

    [Fact]
    public async Task SendAsync_NullRelatedEntity_SetsNullFields()
    {
        NotificationDeliveryContext context = BuildContext(withRelatedEntity: false);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        _captured.ShouldNotBeNull();
        _captured!.RelatedEntityType.ShouldBeNull();
        _captured.RelatedEntityId.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static NotificationDeliveryContext BuildContext(bool withRelatedEntity = true) => new()
    {
        DeliveryId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        RecipientUserId = "user-1",
        Severity = NotificationSeverity.Info,
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        RelatedEntity = withRelatedEntity ? new EntityReference("Patient", "pat-1") : null,
        OccurredAt = DateTimeOffset.UtcNow,
    };
}
