using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

public sealed class AspNetTwoFactorServiceTests
{
    private static readonly string UserId = Guid.NewGuid().ToString();

    private readonly UserManager<GranitUser> _userManager;
    private readonly ITotpService _totpService = Substitute.For<ITotpService>();
    private readonly AspNetTwoFactorService _sut;
    private readonly GranitUser _user;

    public AspNetTwoFactorServiceTests()
    {
        IUserStore<GranitUser> store = Substitute.For<IUserStore<GranitUser>>();
        _userManager = Substitute.For<UserManager<GranitUser>>(
            store, null, null, null, null, null, null, null, null);

        _user = new GranitUser { Id = Guid.Parse(UserId), Email = "user@test.com", UserName = "testuser" };
        _userManager.FindByIdAsync(UserId).Returns(_user);

        _sut = new AspNetTwoFactorService(_userManager, _totpService);
    }

    // --- GetStatusAsync ---

    [Fact]
    public async Task GetStatusAsync_ReturnsCorrectStatus()
    {
        _userManager.GetTwoFactorEnabledAsync(_user).Returns(true);
        _userManager.GetAuthenticatorKeyAsync(_user).Returns("JBSWY3DPEHPK3PXP");
        _userManager.CountRecoveryCodesAsync(_user).Returns(5);

        TwoFactorStatus status = await _sut.GetStatusAsync(UserId, TestContext.Current.CancellationToken);

        status.IsEnabled.ShouldBeTrue();
        status.HasAuthenticatorApp.ShouldBeTrue();
        status.RecoveryCodesLeft.ShouldBe(5);
    }

    [Fact]
    public async Task GetStatusAsync_NoAuthenticatorKey_HasAuthenticatorAppIsFalse()
    {
        _userManager.GetTwoFactorEnabledAsync(_user).Returns(false);
        _userManager.GetAuthenticatorKeyAsync(_user).Returns((string?)null);
        _userManager.CountRecoveryCodesAsync(_user).Returns(0);

        TwoFactorStatus status = await _sut.GetStatusAsync(UserId, TestContext.Current.CancellationToken);

        status.IsEnabled.ShouldBeFalse();
        status.HasAuthenticatorApp.ShouldBeFalse();
        status.RecoveryCodesLeft.ShouldBe(0);
    }

    [Fact]
    public async Task GetStatusAsync_UserNotFound_Throws()
    {
        _userManager.FindByIdAsync("unknown").Returns((GranitUser?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.GetStatusAsync("unknown", TestContext.Current.CancellationToken));
    }

    // --- GetAuthenticatorKeyAsync ---

    [Fact]
    public async Task GetAuthenticatorKeyAsync_KeyExists_ReturnsKeyAndQrCode()
    {
        _userManager.GetAuthenticatorKeyAsync(_user).Returns("EXISTINGKEY");
        _totpService.GetQrCodeUri("user@test.com", "EXISTINGKEY").Returns("otpauth://totp/...");

        AuthenticatorKeyInfo info = await _sut.GetAuthenticatorKeyAsync(UserId, TestContext.Current.CancellationToken);

        info.SharedKey.ShouldBe("EXISTINGKEY");
        info.QrCodeUri.ShouldBe("otpauth://totp/...");
        await _userManager.DidNotReceive().ResetAuthenticatorKeyAsync(_user);
    }

    [Fact]
    public async Task GetAuthenticatorKeyAsync_NoKey_ResetsAndReturnsNewKey()
    {
        _userManager.GetAuthenticatorKeyAsync(_user).Returns(
            (string?)null,    // first call: no key
            "NEWLYGENERATED"  // after reset
        );
        _totpService.GetQrCodeUri("user@test.com", "NEWLYGENERATED").Returns("otpauth://totp/new...");

        AuthenticatorKeyInfo info = await _sut.GetAuthenticatorKeyAsync(UserId, TestContext.Current.CancellationToken);

        info.SharedKey.ShouldBe("NEWLYGENERATED");
        await _userManager.Received(1).ResetAuthenticatorKeyAsync(_user);
    }

    [Fact]
    public async Task GetAuthenticatorKeyAsync_NoEmail_UsesUserNameForQrCode()
    {
        var user = new GranitUser { Id = Guid.NewGuid(), Email = null, UserName = "fallback-user" };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.GetAuthenticatorKeyAsync(user).Returns("KEY123");
        _totpService.GetQrCodeUri("fallback-user", "KEY123").Returns("otpauth://...");

        AuthenticatorKeyInfo info = await _sut.GetAuthenticatorKeyAsync(user.Id.ToString(), TestContext.Current.CancellationToken);

        info.SharedKey.ShouldBe("KEY123");
    }

    // --- EnableAsync ---

    [Fact]
    public async Task EnableAsync_ValidCode_EnablesTwoFactorAndReturnsRecoveryCodes()
    {
        _userManager.GetAuthenticatorKeyAsync(_user).Returns("VALIDKEY");
        _totpService.ValidateCode("VALIDKEY", "123456").Returns(true);
        _userManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10)
            .Returns(new[] { "CODE1", "CODE2", "CODE3" }.AsEnumerable());

        IReadOnlyList<string> codes = await _sut.EnableAsync(UserId, "123456", TestContext.Current.CancellationToken);

        codes.Count.ShouldBe(3);
        codes.ShouldContain("CODE1");
        await _userManager.Received(1).SetTwoFactorEnabledAsync(_user, true);
    }

    [Fact]
    public async Task EnableAsync_InvalidCode_ThrowsInvalidOperation()
    {
        _userManager.GetAuthenticatorKeyAsync(_user).Returns("VALIDKEY");
        _totpService.ValidateCode("VALIDKEY", "000000").Returns(false);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.EnableAsync(UserId, "000000", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Invalid TOTP");
        await _userManager.DidNotReceive().SetTwoFactorEnabledAsync(Arg.Any<GranitUser>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task EnableAsync_NoAuthenticatorKey_ThrowsInvalidOperation()
    {
        _userManager.GetAuthenticatorKeyAsync(_user).Returns((string?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.EnableAsync(UserId, "123456", TestContext.Current.CancellationToken));
    }

    // --- DisableAsync ---

    [Fact]
    public async Task DisableAsync_DisablesTwoFactorAndUpdatesSecurityStamp()
    {
        await _sut.DisableAsync(UserId, TestContext.Current.CancellationToken);

        await _userManager.Received(1).SetTwoFactorEnabledAsync(_user, false);
        await _userManager.Received(1).UpdateSecurityStampAsync(_user);
    }

    // --- ResetAuthenticatorAsync ---

    [Fact]
    public async Task ResetAuthenticatorAsync_ResetsKey()
    {
        await _sut.ResetAuthenticatorAsync(UserId, TestContext.Current.CancellationToken);

        await _userManager.Received(1).ResetAuthenticatorKeyAsync(_user);
    }

    // --- GenerateRecoveryCodesAsync ---

    [Fact]
    public async Task GenerateRecoveryCodesAsync_ReturnsCodes()
    {
        string[] expectedCodes = ["RC1", "RC2", "RC3", "RC4", "RC5"];
        _userManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10)
            .Returns(expectedCodes.AsEnumerable());

        IReadOnlyList<string> codes = await _sut.GenerateRecoveryCodesAsync(UserId, TestContext.Current.CancellationToken);

        codes.Count.ShouldBe(5);
    }

    [Fact]
    public async Task GenerateRecoveryCodesAsync_NullFromManager_ReturnsEmptyList()
    {
        _userManager.GenerateNewTwoFactorRecoveryCodesAsync(_user, 10)
            .Returns((IEnumerable<string>?)null);

        IReadOnlyList<string> codes = await _sut.GenerateRecoveryCodesAsync(UserId, TestContext.Current.CancellationToken);

        codes.ShouldBeEmpty();
    }
}
