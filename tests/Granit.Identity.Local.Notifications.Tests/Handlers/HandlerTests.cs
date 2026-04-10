using Granit.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Notifications.Handlers;
using Granit.Identity.Local.Notifications.NotificationTypes;
using Granit.Identity.Local.Notifications.Options;
using Granit.Notifications;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Notifications.Tests.Handlers;

public sealed class HandlerTests
{
    private readonly INotificationPublisher _publisher = Substitute.For<INotificationPublisher>();
    private readonly IIdentityUserReader _userReader = Substitute.For<IIdentityUserReader>();
    private readonly IOptions<IdentityNotificationOptions> _options = Microsoft.Extensions.Options.Options.Create(new IdentityNotificationOptions
    {
        FrontendBaseUrl = "https://app.example.com",
    });

    // --- UserRegisteredHandler ---

    [Fact]
    public async Task UserRegistered_PublishesWelcome()
    {
        SetupUser("alice@test.com");
        var evt = new UserRegisteredEto(Guid.Parse("00000000-0000-0000-0000-000000000001"), null);

        await UserRegisteredHandler.HandleAsync(evt, _userReader, _publisher, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            WelcomeNotificationType.Instance,
            Arg.Is<WelcomeNotificationData>(d => d.Email == "alice@test.com"),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserRegistered_UserNotFound_DoesNotPublish()
    {
        _userReader.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IIdentityUser?)null);
        var evt = new UserRegisteredEto(Guid.NewGuid(), null);

        await UserRegisteredHandler.HandleAsync(evt, _userReader, _publisher, TestContext.Current.CancellationToken);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<NotificationType<WelcomeNotificationData>>(),
            Arg.Any<WelcomeNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- PasswordResetRequestedHandler ---

    [Fact]
    public async Task PasswordReset_PublishesWithResetLink()
    {
        var evt = new PasswordResetRequestedEto(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "alice@test.com", "reset-token-123", null);

        await PasswordResetRequestedHandler.HandleAsync(evt, _options, _publisher, cancellationToken: TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            PasswordResetNotificationType.Instance,
            Arg.Is<PasswordResetNotificationData>(d =>
                d.Email == "alice@test.com" &&
                d.ResetLink.Contains("reset-token-123")),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // --- EmailConfirmationRequestedHandler ---

    [Fact]
    public async Task EmailConfirmation_PublishesWithConfirmLink()
    {
        SetupUser("alice@test.com");
        var evt = new EmailConfirmationRequestedEto(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "confirm-token-456", null);

        await EmailConfirmationRequestedHandler.HandleAsync(evt, _userReader, _options, _publisher, cancellationToken: TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            EmailConfirmationNotificationType.Instance,
            Arg.Is<EmailConfirmationNotificationData>(d =>
                d.Email == "alice@test.com" &&
                d.ConfirmLink.Contains("confirm-token-456")),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EmailConfirmation_UserNotFound_DoesNotPublish()
    {
        _userReader.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IIdentityUser?)null);
        var evt = new EmailConfirmationRequestedEto(Guid.NewGuid(), "token", null);

        await EmailConfirmationRequestedHandler.HandleAsync(evt, _userReader, _options, _publisher, cancellationToken: TestContext.Current.CancellationToken);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<NotificationType<EmailConfirmationNotificationData>>(),
            Arg.Any<EmailConfirmationNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- EmailChangeRequestedHandler (dual notification) ---

    [Fact]
    public async Task EmailChange_PublishesTwoNotifications()
    {
        var evt = new EmailChangeRequestedEto(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "old@test.com", "new@test.com", "change-token", null);

        await EmailChangeRequestedHandler.HandleAsync(evt, _options, _publisher, cancellationToken: TestContext.Current.CancellationToken);

        // Alert to old email (standard publish)
        await _publisher.Received(1).PublishAsync(
            EmailChangeAlertNotificationType.Instance,
            Arg.Is<EmailChangeAlertNotificationData>(d =>
                d.Email == "old@test.com" && d.NewEmail == "new@test.com"),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());

        // Confirmation to new email (with RecipientOverride)
        await _publisher.Received(1).PublishAsync(
            EmailChangeConfirmationNotificationType.Instance,
            Arg.Is<EmailChangeConfirmationNotificationData>(d =>
                d.NewEmail == "new@test.com" && d.ConfirmLink.Contains("change-token")),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Is<RecipientInfo>(r => r.Email == "new@test.com"),
            Arg.Any<EntityReference?>(),
            Arg.Any<CancellationToken>());
    }

    // --- AccountLockedHandler ---

    [Fact]
    public async Task AccountLocked_PublishesWithFailedAttemptsAndResetLinkAndExpiry()
    {
        DateTimeOffset lockoutEnd = new(2026, 4, 5, 14, 30, 0, TimeSpan.Zero);
        var evt = new AccountLockedEto(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "alice@test.com", 5, "lockout-reset-token", lockoutEnd, null);

        await AccountLockedHandler.HandleAsync(evt, _options, _publisher, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            AccountLockedNotificationType.Instance,
            Arg.Is<AccountLockedNotificationData>(d =>
                d.FailedAttempts == 5 &&
                d.Email == "alice@test.com" &&
                d.ResetLink.Contains("lockout-reset-token") &&
                d.LockoutExpiresAt == lockoutEnd),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // --- TwoFactorChangedHandler ---

    [Fact]
    public async Task TwoFactorChanged_PublishesEnabledState()
    {
        SetupUser("alice@test.com");
        var evt = new TwoFactorChangedEto(
            Guid.Parse("00000000-0000-0000-0000-000000000001"), true, null);

        await TwoFactorChangedHandler.HandleAsync(evt, _userReader, _publisher, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            TwoFactorChangedNotificationType.Instance,
            Arg.Is<TwoFactorChangedNotificationData>(d => d.Enabled == true),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TwoFactorChanged_UserNotFound_DoesNotPublish()
    {
        _userReader.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IIdentityUser?)null);
        var evt = new TwoFactorChangedEto(Guid.NewGuid(), false, null);

        await TwoFactorChangedHandler.HandleAsync(evt, _userReader, _publisher, TestContext.Current.CancellationToken);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<NotificationType<TwoFactorChangedNotificationData>>(),
            Arg.Any<TwoFactorChangedNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- PasswordChangedHandler ---

    [Fact]
    public async Task PasswordChanged_PublishesAlert()
    {
        SetupUser("alice@test.com");
        var evt = new PasswordChangedEto(
            Guid.Parse("00000000-0000-0000-0000-000000000001"), null);

        await PasswordChangedHandler.HandleAsync(evt, _userReader, _publisher, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            PasswordChangedNotificationType.Instance,
            Arg.Is<PasswordChangedNotificationData>(d => d.Email == "alice@test.com"),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PasswordChanged_UserNotFound_DoesNotPublish()
    {
        _userReader.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IIdentityUser?)null);
        var evt = new PasswordChangedEto(Guid.NewGuid(), null);

        await PasswordChangedHandler.HandleAsync(evt, _userReader, _publisher, TestContext.Current.CancellationToken);

        await _publisher.DidNotReceiveWithAnyArgs().PublishAsync(
            Arg.Any<NotificationType<PasswordChangedNotificationData>>(),
            Arg.Any<PasswordChangedNotificationData>(),
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<CancellationToken>());
    }

    // --- UserImpersonatedHandler ---

    [Fact]
    public async Task UserImpersonated_PublishesWithDisplayName()
    {
        IIdentityUser impersonator = Substitute.For<IIdentityUser>();
        impersonator.FirstName.Returns("Admin");
        impersonator.LastName.Returns("User");
        _userReader.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(impersonator);

        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var evt = new UserImpersonatedEto(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            null,
            occurredAt);

        await UserImpersonatedHandler.HandleAsync(evt, _userReader, _publisher, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            ImpersonationAlertNotificationType.Instance,
            Arg.Is<ImpersonationAlertNotificationData>(d =>
                d.OccurredAt == occurredAt &&
                d.ImpersonatorDisplayName == "Admin User"),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserImpersonated_ImpersonatorNotFound_PublishesWithNullDisplayName()
    {
        _userReader.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((IIdentityUser?)null);

        DateTimeOffset occurredAt = DateTimeOffset.UtcNow;
        var evt = new UserImpersonatedEto(
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            null,
            occurredAt);

        await UserImpersonatedHandler.HandleAsync(evt, _userReader, _publisher, TestContext.Current.CancellationToken);

        await _publisher.Received(1).PublishAsync(
            ImpersonationAlertNotificationType.Instance,
            Arg.Is<ImpersonationAlertNotificationData>(d =>
                d.ImpersonatorDisplayName == null),
            Arg.Is<IReadOnlyList<string>>(r => r.Count == 1),
            Arg.Any<CancellationToken>());
    }

    // --- Helper ---

    private void SetupUser(string email)
    {
        IIdentityUser user = Substitute.For<IIdentityUser>();
        user.Email.Returns(email);
        _userReader.GetUserAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(user);
    }
}
