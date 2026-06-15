using System.Security.Claims;
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

    private readonly UserManager<LocalIdentity> _userManager;
    private readonly AspNetTwoFactorService _sut;
    private readonly LocalIdentity _user;

    public AspNetTwoFactorServiceTests()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        _userManager = Substitute.For<UserManager<LocalIdentity>>(
            store, null, null, null, null, null, null, null, null);

        _user = new LocalIdentity { Id = Guid.Parse(UserId), Email = "user@test.com", UserName = "testuser" };
        _userManager.FindByIdAsync(UserId).Returns(_user);

        // Default: no email-OTP claim. Individual tests override.
        _userManager.GetClaimsAsync(_user).Returns([]);

        _sut = new AspNetTwoFactorService(_userManager);
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
        status.HasEmailOtp.ShouldBeFalse();
        status.RecoveryCodesLeft.ShouldBe(5);
    }

    [Fact]
    public async Task GetStatusAsync_WithEmailOtpClaim_HasEmailOtpIsTrue()
    {
        _userManager.GetTwoFactorEnabledAsync(_user).Returns(true);
        _userManager.GetAuthenticatorKeyAsync(_user).Returns((string?)null);
        _userManager.CountRecoveryCodesAsync(_user).Returns(0);
        _userManager.GetClaimsAsync(_user).Returns(
            [new("granit:2fa:email_otp", "true")]);

        TwoFactorStatus status = await _sut.GetStatusAsync(UserId, TestContext.Current.CancellationToken);

        status.HasEmailOtp.ShouldBeTrue();
        status.HasAuthenticatorApp.ShouldBeFalse();
    }

    [Fact]
    public async Task GetStatusAsync_UserNotFound_Throws()
    {
        _userManager.FindByIdAsync("unknown").Returns((LocalIdentity?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.GetStatusAsync("unknown", TestContext.Current.CancellationToken));
    }

    // --- GetAvailableMethodsAsync ---

    [Fact]
    public async Task GetAvailableMethodsAsync_AllFactors_ReturnsAll()
    {
        _userManager.GetTwoFactorEnabledAsync(_user).Returns(true);
        _userManager.GetAuthenticatorKeyAsync(_user).Returns("KEY");
        _userManager.CountRecoveryCodesAsync(_user).Returns(3);
        _userManager.GetClaimsAsync(_user).Returns(
            [new("granit:2fa:email_otp", "true")]);

        IReadOnlyList<TwoFactorMethod> methods = await _sut.GetAvailableMethodsAsync(
            UserId, TestContext.Current.CancellationToken);

        methods.ShouldBe([TwoFactorMethod.Authenticator, TwoFactorMethod.Email, TwoFactorMethod.RecoveryCode]);
    }

    [Fact]
    public async Task GetAvailableMethodsAsync_TwoFactorDisabled_ReturnsEmpty()
    {
        _userManager.GetTwoFactorEnabledAsync(_user).Returns(false);

        IReadOnlyList<TwoFactorMethod> methods = await _sut.GetAvailableMethodsAsync(
            UserId, TestContext.Current.CancellationToken);

        methods.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAvailableMethodsAsync_EmailOnlyNoRecoveryCodes_ReturnsEmailOnly()
    {
        _userManager.GetTwoFactorEnabledAsync(_user).Returns(true);
        _userManager.GetAuthenticatorKeyAsync(_user).Returns((string?)null);
        _userManager.CountRecoveryCodesAsync(_user).Returns(0);
        _userManager.GetClaimsAsync(_user).Returns(
            [new("granit:2fa:email_otp", "true")]);

        IReadOnlyList<TwoFactorMethod> methods = await _sut.GetAvailableMethodsAsync(
            UserId, TestContext.Current.CancellationToken);

        methods.ShouldBe([TwoFactorMethod.Email]);
    }

    // --- DisableAllAsync ---

    [Fact]
    public async Task DisableAllAsync_ClearsEveryFactorAndRotatesStamp()
    {
        _userManager.GetClaimsAsync(_user).Returns(
            [new("granit:2fa:email_otp", "true")]);

        await _sut.DisableAllAsync(UserId, TestContext.Current.CancellationToken);

        await _userManager.Received(1).SetTwoFactorEnabledAsync(_user, false);
        await _userManager.Received(1).ResetAuthenticatorKeyAsync(_user);
        await _userManager.Received(1).RemoveClaimAsync(
            _user, Arg.Is<Claim>(c => c.Type == "granit:2fa:email_otp"));
        await _userManager.Received(1).UpdateSecurityStampAsync(_user);
    }

    [Fact]
    public async Task DisableAllAsync_NoEmailClaim_DoesNotRemoveClaim()
    {
        await _sut.DisableAllAsync(UserId, TestContext.Current.CancellationToken);

        await _userManager.DidNotReceive().RemoveClaimAsync(_user, Arg.Any<Claim>());
        await _userManager.Received(1).SetTwoFactorEnabledAsync(_user, false);
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
