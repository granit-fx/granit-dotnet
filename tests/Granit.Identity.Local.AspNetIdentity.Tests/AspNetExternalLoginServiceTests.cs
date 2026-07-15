using System.Security.Claims;
using Granit.Authentication.External.Options;
using Granit.Events;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;
using GranitExternalLoginInfo = Granit.Identity.Local.Services.ExternalLoginInfo;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

public sealed class AspNetExternalLoginServiceTests
{
    private readonly UserManager<LocalIdentity> _userManager;
    private readonly ExternalClaimsMapper _claimsMapper = Substitute.For<ExternalClaimsMapper>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly ExternalAuthOptions _externalAuthOptions = new() { AutoRegisterExternalUsers = true };
    private readonly AspNetExternalLoginService _sut;

    public AspNetExternalLoginServiceTests()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        _userManager = Substitute.For<UserManager<LocalIdentity>>(
            store, null, null, null, null, null, null, null, null);
        // RequireUniqueEmail gates the "needs profile completion" branch; default options (false)
        // keep the happy path creating directly.
        _userManager.Options = new IdentityOptions();

        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UnixEpoch);

        _sut = new AspNetExternalLoginService(
            _userManager,
            _claimsMapper,
            _eventBus,
            clock,
            Microsoft.Extensions.Options.Options.Create(_externalAuthOptions));
    }

    // --- GetLoginsAsync ---

    [Fact]
    public async Task GetLoginsAsync_UserExists_ReturnsLogins()
    {
        LocalIdentity user = CreateUser();
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.GetLoginsAsync(user).Returns(
        [
            new UserLoginInfo("Google", "g-key", "Google"),
            new UserLoginInfo("Microsoft", "ms-key", "Microsoft"),
        ]);

        IReadOnlyList<GranitExternalLoginInfo> logins =
            await _sut.GetLoginsAsync(user.Id.ToString(), TestContext.Current.CancellationToken);

        logins.Count.ShouldBe(2);
        logins[0].LoginProvider.ShouldBe("Google");
        logins[1].LoginProvider.ShouldBe("Microsoft");
    }

    [Fact]
    public async Task GetLoginsAsync_UserNotFound_ReturnsEmpty()
    {
        _userManager.FindByIdAsync("unknown").Returns((LocalIdentity?)null);

        IReadOnlyList<GranitExternalLoginInfo> logins =
            await _sut.GetLoginsAsync("unknown", TestContext.Current.CancellationToken);

        logins.ShouldBeEmpty();
    }

    // --- AddLoginAsync ---

    [Fact]
    public async Task AddLoginAsync_Success()
    {
        LocalIdentity user = CreateUser();
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.AddLoginAsync(user, Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        await Should.NotThrowAsync(
            () => _sut.AddLoginAsync(
                user.Id.ToString(),
                new GranitExternalLoginInfo("GitHub", "gh-key", "GitHub"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddLoginAsync_UserNotFound_Throws()
    {
        _userManager.FindByIdAsync("unknown").Returns((LocalIdentity?)null);

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.AddLoginAsync(
                "unknown",
                new GranitExternalLoginInfo("GitHub", "gh-key", "GitHub"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AddLoginAsync_IdentityResultFailed_Throws()
    {
        LocalIdentity user = CreateUser();
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.AddLoginAsync(user, Arg.Any<UserLoginInfo>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Duplicate login" }));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.AddLoginAsync(
                user.Id.ToString(),
                new GranitExternalLoginInfo("GitHub", "gh-key", "GitHub"),
                TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Duplicate login");
    }

    // --- RemoveLoginAsync ---

    [Fact]
    public async Task RemoveLoginAsync_HasPasswordAndMultipleLogins_Succeeds()
    {
        LocalIdentity user = CreateUser();
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(true);
        _userManager.GetLoginsAsync(user).Returns([new UserLoginInfo("Google", "key", "Google")]);
        _userManager.RemoveLoginAsync(user, "Google", "key").Returns(IdentityResult.Success);

        await Should.NotThrowAsync(
            () => _sut.RemoveLoginAsync(user.Id.ToString(), "Google", "key", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveLoginAsync_LastLoginWithoutPassword_Throws()
    {
        LocalIdentity user = CreateUser();
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(false);
        _userManager.GetLoginsAsync(user).Returns([new UserLoginInfo("Google", "key", "Google")]);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.RemoveLoginAsync(user.Id.ToString(), "Google", "key", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("last external login");
    }

    [Fact]
    public async Task RemoveLoginAsync_NoPasswordButMultipleLogins_Succeeds()
    {
        LocalIdentity user = CreateUser();
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(false);
        _userManager.GetLoginsAsync(user).Returns(
        [
            new UserLoginInfo("Google", "g-key", "Google"),
            new UserLoginInfo("Microsoft", "ms-key", "Microsoft"),
        ]);
        _userManager.RemoveLoginAsync(user, "Google", "g-key").Returns(IdentityResult.Success);

        await Should.NotThrowAsync(
            () => _sut.RemoveLoginAsync(user.Id.ToString(), "Google", "g-key", TestContext.Current.CancellationToken));
    }

    // --- ProcessCallbackAsync ---

    [Fact]
    public async Task ProcessCallbackAsync_ExistingUserByLogin_ReturnsExistingUser()
    {
        LocalIdentity user = CreateUser();
        ClaimsPrincipal principal = CreatePrincipal("sub", "provider-key-123");
        _userManager.FindByLoginAsync("Google", "provider-key-123").Returns(user);

        ProcessCallbackResult result =
            await _sut.ProcessCallbackAsync(principal, "Google", allowRegistration: true, TestContext.Current.CancellationToken);

        result.UserId.ShouldBe(user.Id);
        result.IsNewUser.ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessCallbackAsync_ExistingUserByEmail_LinksAndReturnsExisting()
    {
        LocalIdentity user = CreateUser();
        ClaimsPrincipal principal = CreatePrincipal("sub", "new-provider-key");
        _userManager.FindByLoginAsync("Google", "new-provider-key").Returns((LocalIdentity?)null);
        _claimsMapper.MapToUserProperties(principal, "Google")
            .Returns(new ExternalUserProperties { Email = "user@test.com" });
        _userManager.FindByEmailAsync("user@test.com").Returns(user);
        _userManager.AddLoginAsync(user, Arg.Any<UserLoginInfo>()).Returns(IdentityResult.Success);

        ProcessCallbackResult result =
            await _sut.ProcessCallbackAsync(principal, "Google", allowRegistration: true, TestContext.Current.CancellationToken);

        result.UserId.ShouldBe(user.Id);
        result.IsNewUser.ShouldBeFalse();
        await _userManager.Received(1).AddLoginAsync(user, Arg.Any<UserLoginInfo>());
    }

    [Fact]
    public async Task ProcessCallbackAsync_AutoRegister_CreatesUserAndPublishesEvent()
    {
        ClaimsPrincipal principal = CreatePrincipal("sub", "brand-new-key");
        _userManager.FindByLoginAsync("Google", "brand-new-key").Returns((LocalIdentity?)null);
        _claimsMapper.MapToUserProperties(principal, "Google")
            .Returns(new ExternalUserProperties
            {
                Email = "new@example.com",
                FirstName = "Jane",
                LastName = "Doe",
            });
        _userManager.FindByEmailAsync("new@example.com").Returns((LocalIdentity?)null);
        _userManager.CreateAsync(Arg.Any<LocalIdentity>()).Returns(IdentityResult.Success);
        _userManager.AddLoginAsync(Arg.Any<LocalIdentity>(), Arg.Any<UserLoginInfo>())
            .Returns(IdentityResult.Success);

        ProcessCallbackResult result =
            await _sut.ProcessCallbackAsync(principal, "Google", allowRegistration: true, TestContext.Current.CancellationToken);

        result.IsNewUser.ShouldBeTrue();
        await _userManager.Received(1).CreateAsync(Arg.Is<LocalIdentity>(u =>
            u.Email == "new@example.com" &&
            u.FirstName == "Jane" &&
            u.LastName == "Doe" &&
            u.EmailConfirmed));
        await _eventBus.Received(1).PublishAsync(
            Arg.Any<UserRegisteredEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessCallbackAsync_AutoRegisterDisabled_Throws()
    {
        _externalAuthOptions.AutoRegisterExternalUsers = false;

        ClaimsPrincipal principal = CreatePrincipal("sub", "new-key");
        _userManager.FindByLoginAsync("Google", "new-key").Returns((LocalIdentity?)null);
        _claimsMapper.MapToUserProperties(principal, "Google")
            .Returns(new ExternalUserProperties { Email = "noone@example.com" });
        _userManager.FindByEmailAsync("noone@example.com").Returns((LocalIdentity?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ProcessCallbackAsync(principal, "Google", allowRegistration: true, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Account not found");
    }

    [Fact]
    public async Task ProcessCallbackAsync_MissingProviderKey_Throws()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity()); // no claims

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ProcessCallbackAsync(principal, "Google", allowRegistration: true, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProcessCallbackAsync_UsesNameIdentifierAsFallback()
    {
        LocalIdentity user = CreateUser();
        ClaimsPrincipal principal = CreatePrincipal(ClaimTypes.NameIdentifier, "ni-key");
        _userManager.FindByLoginAsync("Microsoft", "ni-key").Returns(user);

        ProcessCallbackResult result =
            await _sut.ProcessCallbackAsync(principal, "Microsoft", allowRegistration: true, TestContext.Current.CancellationToken);

        result.UserId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task ProcessCallbackAsync_RegistrationDisabled_NewUser_Throws()
    {
        ClaimsPrincipal principal = CreatePrincipal("sub", "no-account-key");
        _userManager.FindByLoginAsync("Google", "no-account-key").Returns((LocalIdentity?)null);
        _claimsMapper.MapToUserProperties(principal, "Google")
            .Returns(new ExternalUserProperties { Email = "nobody@example.com" });
        _userManager.FindByEmailAsync("nobody@example.com").Returns((LocalIdentity?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ProcessCallbackAsync(principal, "Google", allowRegistration: false, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Account not found");
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<LocalIdentity>());
    }

    [Fact]
    public async Task ProcessCallbackAsync_RequireUniqueEmailButNoEmail_ReturnsNeedsProfile()
    {
        _userManager.Options = new IdentityOptions { User = { RequireUniqueEmail = true } };

        ClaimsPrincipal principal = CreatePrincipal("sub", "no-email-key");
        _userManager.FindByLoginAsync("Google", "no-email-key").Returns((LocalIdentity?)null);
        _claimsMapper.MapToUserProperties(principal, "Google")
            .Returns(new ExternalUserProperties { FirstName = "Ada", LastName = "Lovelace" });

        ProcessCallbackResult result =
            await _sut.ProcessCallbackAsync(principal, "Google", allowRegistration: true, TestContext.Current.CancellationToken);

        result.Status.ShouldBe(ProcessCallbackStatus.NewUserNeedsProfile);
        result.UserId.ShouldBeNull();
        result.Prefill.ShouldNotBeNull();
        result.Prefill!.Provider.ShouldBe("Google");
        result.Prefill.ProviderKey.ShouldBe("no-email-key");
        result.Prefill.Email.ShouldBeNull();
        result.Prefill.FirstName.ShouldBe("Ada");
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<LocalIdentity>());
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<UserRegisteredEto>(), Arg.Any<CancellationToken>());
    }

    private static LocalIdentity CreateUser() => new() { Id = Guid.NewGuid(), Email = "user@test.com" };

    private static ClaimsPrincipal CreatePrincipal(string claimType, string claimValue) =>
        new(new ClaimsIdentity([new Claim(claimType, claimValue)]));
}
