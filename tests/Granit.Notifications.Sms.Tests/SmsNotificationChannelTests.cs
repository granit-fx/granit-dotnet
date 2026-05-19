// =============================================================================
// Tests - SmsNotificationChannel
// =============================================================================
// Verifies the SMS channel implementation: SMS sending via keyed ISmsSender,
// correct field mapping, recipient resolution, and early-return when no phone.
// =============================================================================

using System.Text.Json;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Sms.Internal;
using Granit.Notifications.Sms.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Sms.Tests;

public sealed class SmsNotificationChannelTests
{
    private readonly ISmsSender _smsSender = Substitute.For<ISmsSender>();
    private readonly IRecipientResolver _recipientResolver = Substitute.For<IRecipientResolver>();
    private readonly IKeyedServiceProvider _serviceProvider = Substitute.For<IKeyedServiceProvider>();
    private readonly IOptions<SmsChannelOptions> _options;
    private readonly SmsNotificationChannel _channel;

    public SmsNotificationChannelTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new SmsChannelOptions
        {
            Provider = "Brevo",
            SenderId = "MyApp",
        });

        _serviceProvider.GetRequiredKeyedService(typeof(ISmsSender), "Brevo")
            .Returns(_smsSender);

        _channel = new SmsNotificationChannel(_serviceProvider, _options, _recipientResolver);
    }

    [Fact]
    public async Task SendAsync_WithRecipientPhone_SendsSms()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", phoneNumber: "+32470123456");

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _smsSender.Received(1).SendAsync(
            Arg.Is<SmsMessage>(m =>
                m.To == "+32470123456" &&
                m.Body == "Notification: test.notification"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullRecipient_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        _recipientResolver.ResolveAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((RecipientInfo?)null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _smsSender.DidNotReceive().SendAsync(
            Arg.Any<SmsMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullPhone_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", phoneNumber: null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _smsSender.DidNotReceive().SendAsync(
            Arg.Any<SmsMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SetsSenderIdFromOptions()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", phoneNumber: "+32470123456");
        SmsMessage? captured = null;
        _smsSender.SendAsync(Arg.Any<SmsMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<SmsMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.SenderId.ShouldBe("MyApp");
    }

    [Fact]
    public void Name_ReturnsSms() =>
        _channel.Name.ShouldBe(NotificationChannels.Sms);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void SetupRecipient(string userId, string? phoneNumber) =>
        _recipientResolver.ResolveAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new RecipientInfo
            {
                UserId = userId,
                PhoneNumber = phoneNumber,
            });

    private static NotificationDeliveryContext BuildContext() => new()
    {
        DeliveryId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        RecipientUserId = "user-1",
        Severity = NotificationSeverity.Info,
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        OccurredAt = DateTimeOffset.UtcNow,
    };
}
