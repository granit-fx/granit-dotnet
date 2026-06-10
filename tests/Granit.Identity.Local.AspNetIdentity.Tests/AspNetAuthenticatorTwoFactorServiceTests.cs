using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

public sealed class AspNetAuthenticatorTwoFactorServiceTests
{
    private static readonly string UserId = Guid.NewGuid().ToString();

    private readonly UserManager<LocalIdentity> _userManager;
    private readonly ITotpService _totpService = Substitute.For<ITotpService>();
    private readonly AspNetAuthenticatorTwoFactorService _sut;
    private readonly LocalIdentity _user;

    public AspNetAuthenticatorTwoFactorServiceTests()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        _userManager = Substitute.For<UserManager<LocalIdentity>>(
            store, null, null, null, null, null, null, null, null);

        _user = new LocalIdentity { Id = Guid.Parse(UserId), Email = "user@test.com", UserName = "testuser" };
        _userManager.FindByIdAsync(UserId).Returns(_user);

        _sut = new AspNetAuthenticatorTwoFactorService(_userManager, _totpService);
    }

    // --- GetKeyAsync ---

    [Fact]
    public async Task GetKeyAsync_KeyExists_ReturnsKeyAndQrCode()
    {
        _userManager.GetAuthenticatorKeyAsync(_user).Returns("EXISTINGKEY");
        _totpService.GetQrCodeUri("user@test.com", "EXISTINGKEY").Returns("otpauth://totp/...");

        AuthenticatorKeyInfo info = await _sut.GetKeyAsync(UserId, TestContext.Current.CancellationToken);

        info.SharedKey.ShouldBe("EXISTINGKEY");
        info.QrCodeUri.ShouldBe("otpauth://totp/...");
        await _userManager.DidNotReceive().ResetAuthenticatorKeyAsync(_user);
    }

    [Fact]
    public async Task GetKeyAsync_NoKey_ResetsAndReturnsNewKey()
    {
        _userManager.GetAuthenticatorKeyAsync(_user).Returns(
            (string?)null,
            "NEWLYGENERATED");
        _totpService.GetQrCodeUri("user@test.com", "NEWLYGENERATED").Returns("otpauth://totp/new...");

        AuthenticatorKeyInfo info = await _sut.GetKeyAsync(UserId, TestContext.Current.CancellationToken);

        info.SharedKey.ShouldBe("NEWLYGENERATED");
        await _userManager.Received(1).ResetAuthenticatorKeyAsync(_user);
    }

    [Fact]
    public async Task GetKeyAsync_NoEmail_UsesUserNameForQrCode()
    {
        var user = new LocalIdentity { Id = Guid.NewGuid(), Email = null, UserName = "fallback-user" };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.GetAuthenticatorKeyAsync(user).Returns("KEY123");
        _totpService.GetQrCodeUri("fallback-user", "KEY123").Returns("otpauth://...");

        AuthenticatorKeyInfo info = await _sut.GetKeyAsync(user.Id.ToString(), TestContext.Current.CancellationToken);

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
        await _userManager.DidNotReceive().SetTwoFactorEnabledAsync(Arg.Any<LocalIdentity>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task EnableAsync_NoAuthenticatorKey_ThrowsInvalidOperation()
    {
        _userManager.GetAuthenticatorKeyAsync(_user).Returns((string?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.EnableAsync(UserId, "123456", TestContext.Current.CancellationToken));
    }

    // --- ResetAsync ---

    [Fact]
    public async Task ResetAsync_ResetsKey()
    {
        await _sut.ResetAsync(UserId, TestContext.Current.CancellationToken);

        await _userManager.Received(1).ResetAuthenticatorKeyAsync(_user);
    }
}
