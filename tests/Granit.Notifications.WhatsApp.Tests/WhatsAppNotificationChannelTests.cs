// =============================================================================
// Tests - WhatsAppNotificationChannel
// =============================================================================
// Verifies the WhatsApp channel implementation: message sending via keyed
// IWhatsAppSender, recipient resolution, culture/language fallback chain,
// and early-return when no phone number.
// =============================================================================

using System.Text.Json;
using Granit.Notifications.Abstractions;
using Granit.Notifications.WhatsApp.Internal;
using Granit.Notifications.WhatsApp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.WhatsApp.Tests;

public sealed class WhatsAppNotificationChannelTests
{
    private readonly IWhatsAppSender _whatsAppSender = Substitute.For<IWhatsAppSender>();
    private readonly IRecipientResolver _recipientResolver = Substitute.For<IRecipientResolver>();
    private readonly IKeyedServiceProvider _serviceProvider = Substitute.For<IKeyedServiceProvider>();
    private readonly IOptions<WhatsAppChannelOptions> _options;
    private readonly WhatsAppNotificationChannel _channel;

    public WhatsAppNotificationChannelTests()
    {
        _options = Microsoft.Extensions.Options.Options.Create(new WhatsAppChannelOptions
        {
            Provider = "Brevo",
        });

        _serviceProvider.GetRequiredKeyedService(typeof(IWhatsAppSender), "Brevo")
            .Returns(_whatsAppSender);

        _channel = new WhatsAppNotificationChannel(_serviceProvider, _options, _recipientResolver);
    }

    [Fact]
    public async Task SendAsync_WithRecipientPhone_SendsWhatsApp()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", phoneNumber: "+32470123456");

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _whatsAppSender.Received(1).SendAsync(
            Arg.Is<WhatsAppMessage>(m =>
                m.To == "+32470123456" &&
                m.TemplateName == "test.notification"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullRecipient_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        _recipientResolver.ResolveAsync("user-1", Arg.Any<CancellationToken>())
            .Returns((RecipientInfo?)null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _whatsAppSender.DidNotReceive().SendAsync(
            Arg.Any<WhatsAppMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithNullPhone_DoesNotSend()
    {
        NotificationDeliveryContext context = BuildContext();
        SetupRecipient("user-1", phoneNumber: null);

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        await _whatsAppSender.DidNotReceive().SendAsync(
            Arg.Any<WhatsAppMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_UsesCultureFromContext_WhenProvided()
    {
        NotificationDeliveryContext context = BuildContext(culture: "en");
        SetupRecipient("user-1", phoneNumber: "+32470123456", preferredCulture: "de");
        WhatsAppMessage? captured = null;
        _whatsAppSender.SendAsync(Arg.Any<WhatsAppMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<WhatsAppMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Language.ShouldBe("en");
    }

    [Fact]
    public async Task SendAsync_UsesRecipientPreferredCulture_WhenContextCultureIsNull()
    {
        NotificationDeliveryContext context = BuildContext(culture: null);
        SetupRecipient("user-1", phoneNumber: "+32470123456", preferredCulture: "de");
        WhatsAppMessage? captured = null;
        _whatsAppSender.SendAsync(Arg.Any<WhatsAppMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<WhatsAppMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Language.ShouldBe("de");
    }

    [Fact]
    public async Task SendAsync_DefaultsToFrench_WhenNoCulture()
    {
        NotificationDeliveryContext context = BuildContext(culture: null);
        SetupRecipient("user-1", phoneNumber: "+32470123456", preferredCulture: null);
        WhatsAppMessage? captured = null;
        _whatsAppSender.SendAsync(Arg.Any<WhatsAppMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                captured = callInfo.Arg<WhatsAppMessage>();
                return Task.CompletedTask;
            });

        await _channel.SendAsync(context, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured!.Language.ShouldBe("fr");
    }

    [Fact]
    public void Name_ReturnsWhatsApp() =>
        _channel.Name.ShouldBe(NotificationChannels.WhatsApp);

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void SetupRecipient(string userId, string? phoneNumber, string? preferredCulture = null) =>
        _recipientResolver.ResolveAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new RecipientInfo
            {
                UserId = userId,
                PhoneNumber = phoneNumber,
                PreferredCulture = preferredCulture,
            });

    private static NotificationDeliveryContext BuildContext(string? culture = null) => new()
    {
        DeliveryId = Guid.NewGuid(),
        NotificationId = Guid.NewGuid(),
        NotificationTypeName = "test.notification",
        RecipientUserId = "user-1",
        Severity = NotificationSeverity.Info,
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        OccurredAt = DateTimeOffset.UtcNow,
        Culture = culture,
    };

    // ──── Settings cascade (Tenant/Global) ────

    [Fact]
    public async Task SendAsync_NoTriggerNoRecipientCulture_UsesTenantOrGlobalSetting()
    {
        Granit.Settings.Services.ISettingProvider settings = Substitute.For<Granit.Settings.Services.ISettingProvider>();
        settings.GetOrNullAsync(Granit.Settings.WellKnownSettingNames.PreferredCulture, Arg.Any<CancellationToken>())
            .Returns("nl");
        WhatsAppNotificationChannel channel = new(_serviceProvider, _options, _recipientResolver, settings);
        _recipientResolver.ResolveAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new RecipientInfo { UserId = "user-1", PhoneNumber = "+3225551234" });

        await channel.SendAsync(BuildContext(), TestContext.Current.CancellationToken);

        await _whatsAppSender.Received(1).SendAsync(
            Arg.Is<WhatsAppMessage>(m => m.Language == "nl"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_NoSettingAnywhere_FallsBackToTerminalDefaultLanguage()
    {
        Granit.Settings.Services.ISettingProvider settings = Substitute.For<Granit.Settings.Services.ISettingProvider>();
        settings.GetOrNullAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((string?)null);
        WhatsAppNotificationChannel channel = new(_serviceProvider, _options, _recipientResolver, settings);
        _recipientResolver.ResolveAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new RecipientInfo { UserId = "user-1", PhoneNumber = "+3225551234" });

        await channel.SendAsync(BuildContext(), TestContext.Current.CancellationToken);

        await _whatsAppSender.Received(1).SendAsync(
            Arg.Is<WhatsAppMessage>(m => m.Language == "fr"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_RecipientCulture_TakesPrecedenceOverTenantSetting()
    {
        Granit.Settings.Services.ISettingProvider settings = Substitute.For<Granit.Settings.Services.ISettingProvider>();
        settings.GetOrNullAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("nl");
        WhatsAppNotificationChannel channel = new(_serviceProvider, _options, _recipientResolver, settings);
        _recipientResolver.ResolveAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new RecipientInfo { UserId = "user-1", PhoneNumber = "+3225551234", PreferredCulture = "de" });

        await channel.SendAsync(BuildContext(), TestContext.Current.CancellationToken);

        await _whatsAppSender.Received(1).SendAsync(
            Arg.Is<WhatsAppMessage>(m => m.Language == "de"), Arg.Any<CancellationToken>());
    }
}
