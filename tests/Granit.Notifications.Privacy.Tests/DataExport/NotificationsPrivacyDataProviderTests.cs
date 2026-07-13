using System.Text.Json;
using Granit.Domain.ValueObjects;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Domain;
using Granit.Notifications.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
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
    private readonly IStagedFragmentBuilder _builder = Substitute.For<IStagedFragmentBuilder>();

    private NotificationsPrivacyDataProvider Sut() =>
        new(_notificationReader, _preferenceReader, _subscriptionReader, _currentTenant, _builder);

    private static PrivacyExportContext Ctx(Guid? subjectUserId = null) =>
        new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: subjectUserId ?? Guid.NewGuid(),
            CallerUserId: subjectUserId ?? Guid.NewGuid(),
            TenantId: null,
            Regulation: "EU_GDPR");

    private static StagedExportFragment StubFragment() =>
        new()
        {
            EntryPath = "notifications.json",
            ContentType = "application/json",
            IntegrityTag = "v1:stub",
            StagedBlob = BlobReference.Create(Guid.NewGuid().ToString()),
        };

    private void StubEmptyReaders()
    {
        _notificationReader.GetListAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserNotification>([], 0, HasMore: false));
        _preferenceReader.GetListAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<NotificationPreference>)[]);
        _subscriptionReader.GetUserSubscriptionsAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<NotificationSubscription>)[]);
    }

    [Fact]
    public void ProviderName_Is_Notifications() =>
        NotificationsPrivacyDataProvider.ProviderName.ShouldBe("notifications");

    [Fact]
    public void DisplayKey_TargetsScopeSelectorLocKey() =>
        NotificationsPrivacyDataProvider.DisplayKey.ShouldBe("Privacy.Scopes.Notifications");

    [Fact]
    public void FeatureName_IsNull_AlwaysVisible() =>
        NotificationsPrivacyDataProvider.FeatureName.ShouldBeNull();

    [Fact]
    public async Task HasDataAsync_ReturnsFalse_WhenNothingPresent()
    {
        _currentTenant.IsAvailable.Returns(false);
        StubEmptyReaders();

        bool has = await Sut().HasDataAsync(Ctx(), TestContext.Current.CancellationToken);

        has.ShouldBeFalse();
    }

    [Fact]
    public async Task HasDataAsync_ReturnsTrue_WhenInboxHasAnyItem()
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
            createdAt: DateTimeOffset.UtcNow);
        _notificationReader.GetListAsync(userId.ToString(), null, page: 1, pageSize: 1, Arg.Any<CancellationToken>())
            .Returns(new PagedResult<UserNotification>([notification], 1, HasMore: false));

        bool has = await Sut().HasDataAsync(Ctx(userId), TestContext.Current.CancellationToken);

        has.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_NoData_YieldsNothing()
    {
        _currentTenant.IsAvailable.Returns(false);
        StubEmptyReaders();

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in Sut().ExportAsync(Ctx(), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.ShouldBeEmpty();
        await _builder.DidNotReceive().BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_WithInbox_HandsDtoToBuilder_AndYieldsFragment()
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

        NotificationsExportFragment? capturedDto = null;
        _builder.BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(),
            NotificationsPrivacyDataProvider.ProviderName,
            "notifications.json",
            Arg.Do<NotificationsExportFragment>(d => capturedDto = d),
            Arg.Any<CancellationToken>())
            .Returns(StubFragment());

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in Sut().ExportAsync(Ctx(userId), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.Count.ShouldBe(1);
        fragments[0].ShouldBeOfType<StagedExportFragment>();
        capturedDto.ShouldNotBeNull();
        capturedDto!.UserId.ShouldBe(userId);
        capturedDto.InboxTruncated.ShouldBeFalse();
        capturedDto.Inbox.Count.ShouldBe(1);
        capturedDto.Inbox[0].NotificationTypeName.ShouldBe("welcome");
    }
}
