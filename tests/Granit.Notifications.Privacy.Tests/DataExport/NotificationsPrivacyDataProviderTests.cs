using System.Text.Json;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Privacy.DataExport;
using Granit.QueryEngine;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Privacy.Tests.DataExport;

public sealed class NotificationsPrivacyDataProviderTests
{
    private readonly IUserNotificationReader _notificationReader = Substitute.For<IUserNotificationReader>();
    private readonly INotificationPreferenceReader _preferenceReader = Substitute.For<INotificationPreferenceReader>();
    private readonly INotificationSubscriptionReader _subscriptionReader = Substitute.For<INotificationSubscriptionReader>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();

    private NotificationsPrivacyDataProvider Sut() =>
        new(_notificationReader, _preferenceReader, _subscriptionReader, _currentTenant);

    [Fact]
    public void ProviderName_Is_Notifications() =>
        NotificationsPrivacyDataProvider.ProviderName.ShouldBe("notifications");

    [Fact]
    public void ContentType_IsApplicationJson() =>
        NotificationsPrivacyDataProvider.ContentType.ShouldBe("application/json");

    [Fact]
    public void FileName_IsStable() =>
        NotificationsPrivacyDataProvider.FileName(Guid.NewGuid()).ShouldBe("notifications.json");

    [Fact]
    public async Task ExportAsync_NoData_ReturnsEmpty()
    {
        _currentTenant.IsAvailable.Returns(false);
        _notificationReader.GetListAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserNotification>([], 0, HasMore: false));
        _preferenceReader.GetListAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<NotificationPreference>)[]);
        _subscriptionReader.GetUserSubscriptionsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<NotificationSubscription>)[]);

        ReadOnlyMemory<byte> result = await Sut().ExportAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_WithInboxAndPreferences_ReturnsJsonPayload()
    {
        var userId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(false);

        var notification = UserNotification.Create(
            id: Guid.NewGuid(),
            notificationId: Guid.NewGuid(),
            notificationTypeName: "welcome",
            severity: NotificationSeverity.Info,
            recipientUserId: userId.ToString(),
            data: JsonDocument.Parse("{}").RootElement,
            createdAt: new DateTimeOffset(2026, 4, 19, 10, 0, 0, TimeSpan.Zero));

        _notificationReader.GetListAsync(userId.ToString(), null, 1, Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserNotification>([notification], 1, HasMore: false));
        _preferenceReader.GetListAsync(userId.ToString(), null, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<NotificationPreference>)[]);
        _subscriptionReader.GetUserSubscriptionsAsync(userId.ToString(), null, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<NotificationSubscription>)[]);

        ReadOnlyMemory<byte> result = await Sut().ExportAsync(userId, TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeFalse();
        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("userId").GetGuid().ShouldBe(userId);
        doc.RootElement.GetProperty("inboxTruncated").GetBoolean().ShouldBeFalse();
        doc.RootElement.GetProperty("inbox").GetArrayLength().ShouldBe(1);
        doc.RootElement.GetProperty("inbox")[0]
            .GetProperty("notificationTypeName").GetString().ShouldBe("welcome");
    }
}
