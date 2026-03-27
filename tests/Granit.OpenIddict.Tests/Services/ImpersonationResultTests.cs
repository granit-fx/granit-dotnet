using Granit.Identity.Local.Services;
using Granit.OpenIddict.Services;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Services;

public sealed class ImpersonationResultTests
{
    [Fact]
    public void ImpersonationResult_Properties()
    {
        ImpersonationResult result = new("access-token-value", "refresh-token-value", 3600);

        result.AccessToken.ShouldBe("access-token-value");
        result.RefreshToken.ShouldBe("refresh-token-value");
        result.ExpiresIn.ShouldBe(3600);
    }

    [Fact]
    public void ImpersonationResult_Equality()
    {
        ImpersonationResult a = new("token-a", "refresh-a", 3600);
        ImpersonationResult b = new("token-a", "refresh-a", 3600);

        a.ShouldBe(b);
    }

    [Fact]
    public void ImpersonationResult_Inequality()
    {
        ImpersonationResult a = new("token-a", "refresh-a", 3600);
        ImpersonationResult b = new("token-b", "refresh-b", 1800);

        a.ShouldNotBe(b);
    }
}

public sealed class KeyRotationResultTests
{
    [Fact]
    public void KeyRotationResult_Properties()
    {
        KeyRotationResult result = new(1, 2, 3, 4);

        result.KeysGenerated.ShouldBe(1);
        result.KeysRetired.ShouldBe(2);
        result.KeysRevoked.ShouldBe(3);
        result.KeysPruned.ShouldBe(4);
    }

    [Fact]
    public void KeyRotationResult_Zero_Values()
    {
        KeyRotationResult result = new(0, 0, 0, 0);

        result.KeysGenerated.ShouldBe(0);
        result.KeysRetired.ShouldBe(0);
        result.KeysRevoked.ShouldBe(0);
        result.KeysPruned.ShouldBe(0);
    }

    [Fact]
    public void KeyRotationResult_Equality()
    {
        KeyRotationResult a = new(1, 2, 3, 4);
        KeyRotationResult b = new(1, 2, 3, 4);

        a.ShouldBe(b);
    }
}

public sealed class ProcessCallbackResultTests
{
    [Fact]
    public void ProcessCallbackResult_Properties()
    {
        var userId = Guid.NewGuid();
        ProcessCallbackResult result = new(userId, true);

        result.UserId.ShouldBe(userId);
        result.IsNewUser.ShouldBeTrue();
    }

    [Fact]
    public void ProcessCallbackResult_ExistingUser()
    {
        var userId = Guid.NewGuid();
        ProcessCallbackResult result = new(userId, false);

        result.IsNewUser.ShouldBeFalse();
    }

    [Fact]
    public void ProcessCallbackResult_Equality()
    {
        var userId = Guid.NewGuid();
        ProcessCallbackResult a = new(userId, true);
        ProcessCallbackResult b = new(userId, true);

        a.ShouldBe(b);
    }
}

public sealed class TwoFactorStatusTests
{
    [Fact]
    public void TwoFactorStatus_Properties()
    {
        TwoFactorStatus status = new(true, true, 5);

        status.IsEnabled.ShouldBeTrue();
        status.HasAuthenticatorApp.ShouldBeTrue();
        status.RecoveryCodesLeft.ShouldBe(5);
    }

    [Fact]
    public void TwoFactorStatus_Disabled()
    {
        TwoFactorStatus status = new(false, false, 0);

        status.IsEnabled.ShouldBeFalse();
        status.HasAuthenticatorApp.ShouldBeFalse();
        status.RecoveryCodesLeft.ShouldBe(0);
    }

    [Fact]
    public void TwoFactorStatus_Equality()
    {
        TwoFactorStatus a = new(true, true, 5);
        TwoFactorStatus b = new(true, true, 5);

        a.ShouldBe(b);
    }
}

public sealed class AuthenticatorKeyInfoTests
{
    [Fact]
    public void AuthenticatorKeyInfo_Properties()
    {
        AuthenticatorKeyInfo info = new("JBSWY3DPEHPK3PXP", "otpauth://totp/App:user@test.com?secret=JBSWY3DPEHPK3PXP");

        info.SharedKey.ShouldBe("JBSWY3DPEHPK3PXP");
        info.QrCodeUri.ShouldBe("otpauth://totp/App:user@test.com?secret=JBSWY3DPEHPK3PXP");
    }

    [Fact]
    public void AuthenticatorKeyInfo_Equality()
    {
        AuthenticatorKeyInfo a = new("KEY1", "URI1");
        AuthenticatorKeyInfo b = new("KEY1", "URI1");

        a.ShouldBe(b);
    }
}

public sealed class PasskeyInfoTests
{
    [Fact]
    public void PasskeyInfo_Properties()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var lastUsedAt = new DateTimeOffset(2026, 3, 20, 14, 30, 0, TimeSpan.Zero);

        PasskeyInfo info = new(id, "My YubiKey", createdAt, lastUsedAt);

        info.Id.ShouldBe(id);
        info.Name.ShouldBe("My YubiKey");
        info.CreatedAt.ShouldBe(createdAt);
        info.LastUsedAt.ShouldBe(lastUsedAt);
    }

    [Fact]
    public void PasskeyInfo_NullName()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

        PasskeyInfo info = new(id, null, createdAt, null);

        info.Name.ShouldBeNull();
        info.LastUsedAt.ShouldBeNull();
    }

    [Fact]
    public void PasskeyInfo_Equality()
    {
        var id = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        PasskeyInfo a = new(id, "Key", createdAt, null);
        PasskeyInfo b = new(id, "Key", createdAt, null);

        a.ShouldBe(b);
    }
}

public sealed class UserSessionActivityTests
{
    [Fact]
    public void UserSessionActivity_Properties()
    {
        var lastActivity = new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero);

        UserSessionActivity activity = new("user-123", "jti-abc", lastActivity);

        activity.UserId.ShouldBe("user-123");
        activity.Jti.ShouldBe("jti-abc");
        activity.LastActivityAt.ShouldBe(lastActivity);
    }

    [Fact]
    public void UserSessionActivity_Equality()
    {
        var lastActivity = new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero);
        UserSessionActivity a = new("user-1", "jti-1", lastActivity);
        UserSessionActivity b = new("user-1", "jti-1", lastActivity);

        a.ShouldBe(b);
    }

    [Fact]
    public void UserSessionActivity_Inequality()
    {
        var lastActivity = new DateTimeOffset(2026, 3, 21, 12, 0, 0, TimeSpan.Zero);
        UserSessionActivity a = new("user-1", "jti-1", lastActivity);
        UserSessionActivity b = new("user-2", "jti-2", lastActivity);

        a.ShouldNotBe(b);
    }
}

public sealed class ExternalLoginInfoTests
{
    [Fact]
    public void ExternalLoginInfo_Properties()
    {
        ExternalLoginInfo info = new("Google", "google-key-123", "Google");

        info.LoginProvider.ShouldBe("Google");
        info.ProviderKey.ShouldBe("google-key-123");
        info.ProviderDisplayName.ShouldBe("Google");
    }

    [Fact]
    public void ExternalLoginInfo_NullDisplayName()
    {
        ExternalLoginInfo info = new("GitHub", "gh-key-456", null);

        info.ProviderDisplayName.ShouldBeNull();
    }

    [Fact]
    public void ExternalLoginInfo_Equality()
    {
        ExternalLoginInfo a = new("Google", "key-1", "Google");
        ExternalLoginInfo b = new("Google", "key-1", "Google");

        a.ShouldBe(b);
    }
}
