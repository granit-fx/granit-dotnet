using Granit.Notifications.Abstractions;
using Granit.Privacy.DataDeletion.Events;
using Granit.Privacy.Notifications.Handlers;
using Granit.Privacy.Regulations;
using Granit.Privacy.Regulations.Profiles;
using NSubstitute;
using Xunit;

namespace Granit.Privacy.Notifications.Tests.Handlers;

public sealed class DeletionAcknowledgedHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithGdprRegulation_PublishesWith30DayDeadline()
    {
        // GDPR's regulatory profile is the canonical "1 month" — Art. 12 §3 expressed
        // as 30 calendar days. The handler must surface this through the data record
        // so the email body quotes the correct number for the recipient.
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IRegulationProfileRegistry registry = Substitute.For<IRegulationProfileRegistry>();
        registry.GetProfile(Arg.Is<PrivacyRegulation>(r => r.Value == "EU_GDPR"))
            .Returns(BuildProfile(PrivacyRegulation.EuGdpr, deletionRequestDays: 30));

        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        PersonalDataDeletionRequestedEto evt = new(
            RequestId: requestId,
            UserId: userId,
            RequestedBy: "self",
            RequestedAt: requestedAt,
            Reason: "user-initiated",
            Regulation: "EU_GDPR");

        await DeletionAcknowledgedHandler.HandleAsync(evt, publisher, registry, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionAcknowledgedNotificationType.Instance,
            Arg.Is<PrivacyDeletionAcknowledgedNotificationData>(d =>
                d.RequestId == requestId &&
                d.RequestedAt == requestedAt &&
                d.Regulation == "EU_GDPR" &&
                d.ResponseDeadlineDays == 30),
            Arg.Is<IReadOnlyList<string>>(ids => ids.Count == 1 && ids[0] == userId.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithLgpdRegulation_PublishesWith15DayDeadline()
    {
        // LGPD Art. 19 mandates 15 days — half the GDPR window. This is the bug-driver
        // scenario: pre-fix the email cited "one month" regardless of regulation,
        // misleading Brazilian data subjects on their statutory rights timeline.
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IRegulationProfileRegistry registry = Substitute.For<IRegulationProfileRegistry>();
        registry.GetProfile(Arg.Is<PrivacyRegulation>(r => r.Value == "BR_LGPD"))
            .Returns(BuildProfile(PrivacyRegulation.BrLgpd, deletionRequestDays: 15));

        var userId = Guid.NewGuid();
        var requestId = Guid.NewGuid();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;
        PersonalDataDeletionRequestedEto evt = new(
            RequestId: requestId,
            UserId: userId,
            RequestedBy: "self",
            RequestedAt: requestedAt,
            Reason: "user-initiated",
            Regulation: "BR_LGPD");

        await DeletionAcknowledgedHandler.HandleAsync(evt, publisher, registry, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionAcknowledgedNotificationType.Instance,
            Arg.Is<PrivacyDeletionAcknowledgedNotificationData>(d =>
                d.Regulation == "BR_LGPD" &&
                d.ResponseDeadlineDays == 15),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithUnknownRegulation_FallsBackToGdpr30Days()
    {
        // Unknown regulation codes (typo, missing profile registration, custom Tier 3)
        // must not break the acknowledgement. Falling back to GDPR's 30 days preserves
        // the historical pre-fix behaviour for hosts that have not configured profiles.
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IRegulationProfileRegistry registry = Substitute.For<IRegulationProfileRegistry>();
        registry.GetProfile(Arg.Any<PrivacyRegulation>()).Returns((PrivacyRegulationProfile?)null);

        PersonalDataDeletionRequestedEto evt = new(
            RequestId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RequestedBy: "self",
            RequestedAt: DateTimeOffset.UtcNow,
            Reason: "user-initiated",
            Regulation: "XX_UNKNOWN");

        await DeletionAcknowledgedHandler.HandleAsync(evt, publisher, registry, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionAcknowledgedNotificationType.Instance,
            Arg.Is<PrivacyDeletionAcknowledgedNotificationData>(d => d.ResponseDeadlineDays == 30),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithProfileMissingDeletionDays_FallsBackToGdpr30Days()
    {
        // Some regulations (e.g. CCPA's "without undue delay") do not pin a numeric
        // DeletionRequestDays. We treat null as "use the GDPR default" rather than
        // crashing or quoting "0 days" in the email.
        INotificationPublisher publisher = Substitute.For<INotificationPublisher>();
        IRegulationProfileRegistry registry = Substitute.For<IRegulationProfileRegistry>();
        registry.GetProfile(Arg.Any<PrivacyRegulation>())
            .Returns(BuildProfile(PrivacyRegulation.UsCcpa, deletionRequestDays: null));

        PersonalDataDeletionRequestedEto evt = new(
            RequestId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            RequestedBy: "self",
            RequestedAt: DateTimeOffset.UtcNow,
            Reason: "user-initiated",
            Regulation: "US_CCPA");

        await DeletionAcknowledgedHandler.HandleAsync(evt, publisher, registry, CancellationToken.None);

        await publisher.Received(1).PublishAsync(
            PrivacyDeletionAcknowledgedNotificationType.Instance,
            Arg.Is<PrivacyDeletionAcknowledgedNotificationData>(d => d.ResponseDeadlineDays == 30),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    private static PrivacyRegulationProfile BuildProfile(PrivacyRegulation regulation, int? deletionRequestDays) =>
        new()
        {
            Regulation = regulation,
            DisplayName = regulation.Value,
            JurisdictionCode = "TEST",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases = [],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = deletionRequestDays,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 90,
            CookieConsentModel = ConsentModel.OptIn,
        };
}
