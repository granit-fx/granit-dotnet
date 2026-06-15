using System.Security.Claims;
using Granit.Events;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

public sealed class AspNetEmailTwoFactorServiceTests
{
    private const string EmailOtpClaim = "granit:2fa:email_otp";
    private static readonly string UserId = Guid.NewGuid().ToString();

    private readonly UserManager<LocalIdentity> _userManager;
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly AspNetEmailTwoFactorService _sut;
    private readonly LocalIdentity _user;

    public AspNetEmailTwoFactorServiceTests()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        _userManager = Substitute.For<UserManager<LocalIdentity>>(
            store, null, null, null, null, null, null, null, null);

        _user = new LocalIdentity { Id = Guid.Parse(UserId), Email = "user@test.com", UserName = "testuser" };
        _userManager.FindByIdAsync(UserId).Returns(_user);
        _userManager.GetClaimsAsync(_user).Returns([]);

        _sut = new AspNetEmailTwoFactorService(_userManager, _eventBus);
    }

    // --- SendCodeAsync ---

    [Fact]
    public async Task SendCodeAsync_GeneratesTokenAndPublishesEvent()
    {
        _userManager.GenerateTwoFactorTokenAsync(_user, TokenOptions.DefaultEmailProvider)
            .Returns("654321");

        await _sut.SendCodeAsync(UserId, TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<TwoFactorEmailOtpRequestedEto>(e =>
                e.UserId == _user.Id && e.Code == "654321"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendCodeAsync_NoEmail_DoesNotPublish()
    {
        var user = new LocalIdentity { Id = Guid.NewGuid(), Email = null, UserName = "no-email" };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);

        await _sut.SendCodeAsync(user.Id.ToString(), TestContext.Current.CancellationToken);

        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<TwoFactorEmailOtpRequestedEto>(), Arg.Any<CancellationToken>());
    }

    // --- EnableAsync ---

    [Fact]
    public async Task EnableAsync_ValidCode_FirstFactor_AddsClaimAndEnablesMaster()
    {
        _userManager.VerifyTwoFactorTokenAsync(_user, TokenOptions.DefaultEmailProvider, "123456")
            .Returns(true);
        _userManager.GetTwoFactorEnabledAsync(_user).Returns(false);

        await _sut.EnableAsync(UserId, "123456", TestContext.Current.CancellationToken);

        await _userManager.Received(1).AddClaimAsync(
            _user, Arg.Is<Claim>(c => c.Type == EmailOtpClaim && c.Value == "true"));
        await _userManager.Received(1).SetTwoFactorEnabledAsync(_user, true);
    }

    [Fact]
    public async Task EnableAsync_ValidCode_MasterAlreadyOn_DoesNotToggleMaster()
    {
        _userManager.VerifyTwoFactorTokenAsync(_user, TokenOptions.DefaultEmailProvider, "123456")
            .Returns(true);
        _userManager.GetTwoFactorEnabledAsync(_user).Returns(true);

        await _sut.EnableAsync(UserId, "123456", TestContext.Current.CancellationToken);

        await _userManager.Received(1).AddClaimAsync(_user, Arg.Any<Claim>());
        await _userManager.DidNotReceive().SetTwoFactorEnabledAsync(_user, Arg.Any<bool>());
    }

    [Fact]
    public async Task EnableAsync_InvalidCode_Throws()
    {
        _userManager.VerifyTwoFactorTokenAsync(_user, TokenOptions.DefaultEmailProvider, "000000")
            .Returns(false);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.EnableAsync(UserId, "000000", TestContext.Current.CancellationToken));

        await _userManager.DidNotReceive().AddClaimAsync(Arg.Any<LocalIdentity>(), Arg.Any<Claim>());
    }

    // --- DisableAsync ---

    [Fact]
    public async Task DisableAsync_NoOtherFactor_RemovesClaimAndDisablesMaster()
    {
        _userManager.GetClaimsAsync(_user).Returns([new(EmailOtpClaim, "true")]);
        _userManager.GetAuthenticatorKeyAsync(_user).Returns((string?)null);

        await _sut.DisableAsync(UserId, TestContext.Current.CancellationToken);

        await _userManager.Received(1).RemoveClaimAsync(
            _user, Arg.Is<Claim>(c => c.Type == EmailOtpClaim));
        await _userManager.Received(1).SetTwoFactorEnabledAsync(_user, false);
        await _userManager.Received(1).UpdateSecurityStampAsync(_user);
    }

    [Fact]
    public async Task DisableAsync_AuthenticatorStillActive_KeepsMasterOn()
    {
        _userManager.GetClaimsAsync(_user).Returns([new(EmailOtpClaim, "true")]);
        _userManager.GetAuthenticatorKeyAsync(_user).Returns("STILL-HAS-AUTHENTICATOR");

        await _sut.DisableAsync(UserId, TestContext.Current.CancellationToken);

        await _userManager.Received(1).RemoveClaimAsync(_user, Arg.Any<Claim>());
        await _userManager.DidNotReceive().SetTwoFactorEnabledAsync(_user, Arg.Any<bool>());
        await _userManager.DidNotReceive().UpdateSecurityStampAsync(_user);
    }

    // --- IsEnabledAsync ---

    [Fact]
    public async Task IsEnabledAsync_ClaimPresent_ReturnsTrue()
    {
        _userManager.GetClaimsAsync(_user).Returns([new(EmailOtpClaim, "true")]);

        bool enabled = await _sut.IsEnabledAsync(UserId, TestContext.Current.CancellationToken);

        enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task IsEnabledAsync_NoClaim_ReturnsFalse()
    {
        bool enabled = await _sut.IsEnabledAsync(UserId, TestContext.Current.CancellationToken);

        enabled.ShouldBeFalse();
    }
}
