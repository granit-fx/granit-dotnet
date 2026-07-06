// =============================================================================
// Tests - WebhookSigningKey + WebhookSubscription dual-key delivery model (FU-1a)
// =============================================================================
// Verifies overlap rotation: previous Active key transitions to Retired, the new
// Active key takes over, IsAcceptableAt rules, and last-active revocation guard.
// =============================================================================

using Granit.Webhooks.Domain;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class WebhookSigningKeyTests
{
    private static readonly DateTimeOffset Now = new(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Grace = TimeSpan.FromHours(24);

    [Fact]
    public void RotateSigningKey_RetiresPreviousActive_WithGracePeriod()
    {
        WebhookSubscription subscription = WithSigningKey(out Guid firstKeyId);

        DateTimeOffset rotationAt = Now.AddDays(30);
        subscription.RotateSigningKey(Guid.NewGuid(), "second-protected", rotationAt, Grace);

        subscription.SigningKeys.Count.ShouldBe(2);

        WebhookSigningKey retired = subscription.SigningKeys.Single(k => k.Id == firstKeyId);
        retired.Status.ShouldBe(WebhookSigningKeyStatus.Retired);
        retired.ExpiresAt.ShouldBe(rotationAt + Grace);

        WebhookSigningKey active = subscription.SigningKeys.Single(k => k.Status == WebhookSigningKeyStatus.Active);
        active.ProtectedSecret.ShouldBe("second-protected");
    }

    [Fact]
    public void IsAcceptableAt_Active_AlwaysTrue()
    {
        WebhookSubscription subscription = WithSigningKey(out _);
        WebhookSigningKey active = subscription.SigningKeys[0];

        active.IsAcceptableAt(Now).ShouldBeTrue();
        active.IsAcceptableAt(Now.AddYears(10)).ShouldBeTrue();
    }

    [Fact]
    public void IsAcceptableAt_Retired_TrueWithinGrace_FalseAfter()
    {
        WebhookSubscription subscription = WithSigningKey(out _);

        DateTimeOffset rotationAt = Now;
        subscription.RotateSigningKey(Guid.NewGuid(), "new-protected", rotationAt, Grace);

        WebhookSigningKey retired = subscription.SigningKeys.Single(k => k.Status == WebhookSigningKeyStatus.Retired);
        retired.IsAcceptableAt(rotationAt).ShouldBeTrue();
        retired.IsAcceptableAt(rotationAt + Grace - TimeSpan.FromMinutes(1)).ShouldBeTrue();
        retired.IsAcceptableAt(rotationAt + Grace + TimeSpan.FromMinutes(1)).ShouldBeFalse();
    }

    [Fact]
    public void RevokeSigningKey_LastActive_Throws()
    {
        WebhookSubscription subscription = WithSigningKey(out Guid keyId);

        Should.Throw<InvalidOperationException>(() =>
            subscription.RevokeSigningKey(keyId, Now));
    }

    [Fact]
    public void RevokeSigningKey_RetiredKey_RevokesAndIsRejected()
    {
        WebhookSubscription subscription = WithSigningKey(out Guid firstKeyId);
        subscription.RotateSigningKey(Guid.NewGuid(), "new-protected", Now, Grace);

        bool revoked = subscription.RevokeSigningKey(firstKeyId, Now);

        revoked.ShouldBeTrue();
        WebhookSigningKey reread = subscription.SigningKeys.Single(k => k.Id == firstKeyId);
        reread.Status.ShouldBe(WebhookSigningKeyStatus.Revoked);
        reread.RevokedAt.ShouldBe(Now);
        reread.IsAcceptableAt(Now).ShouldBeFalse();
        reread.IsAcceptableAt(Now.AddYears(1)).ShouldBeFalse();
    }

    [Fact]
    public void RevokeSigningKey_UnknownId_ReturnsFalse()
    {
        WebhookSubscription subscription = WithSigningKey(out _);

        bool revoked = subscription.RevokeSigningKey(Guid.NewGuid(), Now);

        revoked.ShouldBeFalse();
    }

    [Fact]
    public void Revoke_AlreadyRevoked_Throws()
    {
        WebhookSubscription subscription = WithSigningKey(out Guid firstKeyId);
        subscription.RotateSigningKey(Guid.NewGuid(), "new-protected", Now, Grace);
        subscription.RevokeSigningKey(firstKeyId, Now);

        Should.Throw<InvalidOperationException>(() =>
            subscription.RevokeSigningKey(firstKeyId, Now));
    }

    [Fact]
    public void RotateSigningKey_WithHint_UpdatesSubscriptionSigningSecretHint()
    {
        var subscription = WebhookSubscription.Create(
            Guid.NewGuid(),
            "https://example.com/webhook",
            "test.event",
            Guid.NewGuid(),
            "first-protected",
            Now,
            tenantId: null,
            signingSecretHint: "whsec_aaaa****************aaaa");

        subscription.RotateSigningKey(
            Guid.NewGuid(),
            "new-protected",
            Now,
            Grace,
            newSigningSecretHint: "whsec_bbbb****************bbbb");

        subscription.SigningSecretHint.ShouldBe("whsec_bbbb****************bbbb");
    }

    [Fact]
    public void RotateSigningKey_WithoutHint_LeavesExistingSigningSecretHintUntouched()
    {
        var subscription = WebhookSubscription.Create(
            Guid.NewGuid(),
            "https://example.com/webhook",
            "test.event",
            Guid.NewGuid(),
            "first-protected",
            Now,
            tenantId: null,
            signingSecretHint: "whsec_aaaa****************aaaa");

        subscription.RotateSigningKey(Guid.NewGuid(), "new-protected", Now, Grace);

        subscription.SigningSecretHint.ShouldBe("whsec_aaaa****************aaaa");
    }

    [Fact]
    public void StampRotationNotification_RecordsTimestamp()
    {
        WebhookSubscription subscription = WithSigningKey(out _);
        WebhookSigningKey key = subscription.SigningKeys[0];

        key.LastRotationNotificationAt.ShouldBeNull();

        key.StampRotationNotification(Now);

        key.LastRotationNotificationAt.ShouldBe(Now);
    }

    private static WebhookSubscription WithSigningKey(out Guid initialKeyId)
    {
        initialKeyId = Guid.NewGuid();
        return WebhookSubscription.Create(
            Guid.NewGuid(),
            "https://example.com/webhook",
            "test.event",
            initialKeyId,
            "first-protected",
            Now);
    }
}
