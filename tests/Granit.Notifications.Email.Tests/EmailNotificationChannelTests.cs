// =============================================================================
// Tests - EmailNotificationChannel
// =============================================================================
// Verifies the Email channel implementation: email sending via keyed IEmailSender,
// correct field mapping, recipient resolution, and early-return when no email.
// =============================================================================

using System.Text.Json;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Email;
using Granit.Notifications.Email.Internal;
using Granit.Notifications.Email.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Email.Tests;

public sealed class EmailNotificationChannelTests
{
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IRecipientResolver _recipientResolver = Substitute.For<IRecipientResolver>();
    private readonly IKeyedServiceProvider _serviceProvider = Substitute.For<IKeyedServiceProvider>();
    private readonly IOptions<EmailChannelOptions> _options;
    private readonly EmailNotificationChannel _channel;

    public EmailNotificationChannelTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new EmailChannelOptions
        {
            Provider = "Smtp",
            SenderAddress = "no-reply@test.com",
        });

        _serviceProvider.GetRequiredKeyedService(typeof(IEmailSender), "Smtp")
            .Returns(_emailSender);

        _channel = new EmailNotificationChannel(
            _serviceProvider,
            _options,
            _recipientResolver,
            Substitute.For<ILogger<EmailNotificationChannel>>());
    }

    [Fact]
    public async Task SendAsync_WithRecipientEmail_SendsEmail()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _emailSender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.To == "user@test.com" &&
                m.Subject == "test notification" &&
                m.HtmlBody.Contains("test.notification")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullRecipient_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        _recipientResolver.ResolveAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((RecipientInfo?)null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullEmail_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _emailSender.DidNotReceive().SendAsync(
            Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_SetsFromOverrideFromOptions()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", email: "user@test.com");
        EmailMessage? captured = null;
        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<EmailMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.FromOverride.ShouldBe("no-reply@test.com");
    }

    [Fact]
    public void Name_ReturnsEmail() =>
        _channel.Name.ShouldBe(NotificationChannels.Email);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void SetupRecipient(string userId, string? email) =>
        _recipientResolver.ResolveAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new RecipientInfo
            {
                UserId = userId,
                Email = email,
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
