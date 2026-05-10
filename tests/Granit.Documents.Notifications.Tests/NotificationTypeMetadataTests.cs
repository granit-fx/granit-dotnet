using Granit.Documents.Domain;
using Granit.Documents.Events;
using Granit.Documents.Notifications.Handlers;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Documents.Notifications.Tests;

public sealed class NotificationTypeMetadataTests
{
    [Theory]
    [InlineData("documents.shared")]
    [InlineData("documents.share_revoked")]
    [InlineData("documents.quota_warning")]
    [InlineData("documents.quota_exceeded")]
    public void Names_should_match_snake_case_convention(string expected)
    {
        string[] all =
        [
            DocumentSharedNotificationType.Instance.Name,
            DocumentShareRevokedNotificationType.Instance.Name,
            QuotaWarningNotificationType.Instance.Name,
            QuotaExceededNotificationType.Instance.Name,
        ];
        all.ShouldContain(expected);
    }

    [Fact]
    public void Quota_notifications_should_use_higher_severities()
    {
        QuotaWarningNotificationType.Instance.DefaultSeverity.ShouldBe(NotificationSeverity.Warning);
        QuotaExceededNotificationType.Instance.DefaultSeverity.ShouldBe(NotificationSeverity.Error);
    }

    [Fact]
    public async Task DocumentSharedHandler_should_skip_non_user_grants()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var evt = new DocumentShareGrantedEvent(
            ShareId: Guid.NewGuid(),
            TenantId: null,
            TargetType: ShareTargetType.Folder,
            FolderId: Guid.NewGuid(),
            DocumentId: null,
            GranteeType: ShareGranteeType.Role,
            GranteeId: Guid.NewGuid(),
            Permission: SharePermissionLevel.Read,
            IsDefault: true,
            ExpiresAt: null);

        await DocumentSharedHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

        await publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<NotificationType<DocumentSharedNotificationData>>(),
            Arg.Any<DocumentSharedNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DocumentShareRevokedHandler_should_publish_for_user_grants()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        var grantee = Guid.NewGuid();
        var evt = new DocumentShareRevokedEvent(
            ShareId: Guid.NewGuid(),
            TenantId: null,
            TargetType: ShareTargetType.Document,
            FolderId: null,
            DocumentId: Guid.NewGuid(),
            GranteeType: ShareGranteeType.User,
            GranteeId: grantee);

        await DocumentShareRevokedHandler.HandleAsync(evt, publisher, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishAsync(
            DocumentShareRevokedNotificationType.Instance,
            Arg.Any<DocumentShareRevokedNotificationData>(),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1 && r[0] == grantee.ToString()),
            Arg.Any<CancellationToken>());
    }
}
