using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Granit.UserSessions.Notifications.Handlers;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.UserSessions.Notifications.Tests.Handlers;

/// <summary>
/// Verifies the level-based routing: High → the hard-locked suspicious-session alert,
/// anything below High → the opt-out-able new-session review. Both publish to the subject only.
/// </summary>
public sealed class SuspiciousUserSessionDetectedHandlerTests
{
    private static SuspiciousUserSessionDetectedEto Eto(
        UserSessionRiskLevel level,
        string userId = "user-123",
        IReadOnlyList<string>? reasons = null) =>
        new(
            userId,
            "session-1",
            TenantId: Guid.NewGuid(),
            level,
            reasons ?? ["impossible_travel", "new_country"],
            RiskScore: 0.9,
            City: "Brussels",
            CountryCode: "BE",
            UserAgent: "Mozilla/5.0",
            IpAddress: null,
            DetectedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task High_PublishesSuspiciousSessionAlert_ToSubject()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();

        await SuspiciousUserSessionDetectedHandler.HandleAsync(
            Eto(UserSessionRiskLevel.High), publisher, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishAsync(
            SuspiciousUserSessionNotificationType.Instance,
            Arg.Any<SuspiciousUserSessionNotificationData>(),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1 && r[0] == "user-123"),
            Arg.Any<CancellationToken>());

        await publisher.DidNotReceive().PublishAsync(
            NewUserSessionReviewNotificationType.Instance,
            Arg.Any<SuspiciousUserSessionNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserSessionRiskLevel.Medium)]
    [InlineData(UserSessionRiskLevel.Low)]
    public async Task BelowHigh_PublishesNewSessionReview_ToSubject(UserSessionRiskLevel level)
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();

        await SuspiciousUserSessionDetectedHandler.HandleAsync(
            Eto(level), publisher, TestContext.Current.CancellationToken);

        await publisher.Received(1).PublishAsync(
            NewUserSessionReviewNotificationType.Instance,
            Arg.Any<SuspiciousUserSessionNotificationData>(),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1 && r[0] == "user-123"),
            Arg.Any<CancellationToken>());

        await publisher.DidNotReceive().PublishAsync(
            SuspiciousUserSessionNotificationType.Instance,
            Arg.Any<SuspiciousUserSessionNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Data_CarriesPrimaryReasonAndJoinedReasons()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        SuspiciousUserSessionNotificationData? captured = null;
        await publisher.PublishAsync(
            Arg.Any<NotificationType<SuspiciousUserSessionNotificationData>>(),
            Arg.Do<SuspiciousUserSessionNotificationData>(d => captured = d),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());

        await SuspiciousUserSessionDetectedHandler.HandleAsync(
            Eto(UserSessionRiskLevel.High), publisher, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Reason.ShouldBe("impossible_travel");
        captured.ReasonsDisplay.ShouldBe("impossible_travel, new_country");
        captured.City.ShouldBe("Brussels");
        captured.CountryCode.ShouldBe("BE");
        captured.Device.ShouldBe("Mozilla/5.0");
    }

    [Fact]
    public async Task EmptyReasons_FallsBackToAnomaly()
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        SuspiciousUserSessionNotificationData? captured = null;
        await publisher.PublishAsync(
            Arg.Any<NotificationType<SuspiciousUserSessionNotificationData>>(),
            Arg.Do<SuspiciousUserSessionNotificationData>(d => captured = d),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());

        await SuspiciousUserSessionDetectedHandler.HandleAsync(
            Eto(UserSessionRiskLevel.Medium, reasons: []), publisher, TestContext.Current.CancellationToken);

        captured.ShouldNotBeNull();
        captured.Reason.ShouldBe("anomaly");
        captured.ReasonsDisplay.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("")]
    public async Task EmptyUserId_IsGracefulNoOp(string userId)
    {
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();

        await SuspiciousUserSessionDetectedHandler.HandleAsync(
            Eto(UserSessionRiskLevel.High, userId: userId), publisher, TestContext.Current.CancellationToken);

        publisher.ReceivedCalls().ShouldBeEmpty();
    }
}
