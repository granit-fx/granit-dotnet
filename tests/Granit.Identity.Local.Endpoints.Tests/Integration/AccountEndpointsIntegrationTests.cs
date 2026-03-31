using System.Net;
using System.Net.Http.Json;
using Granit.Identity;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;
using ZiggyCreatures.Caching.Fusion;

namespace Granit.Identity.Local.Endpoints.Tests.Integration;

public sealed class AccountEndpointsIntegrationTests : IAsyncLifetime
{
    private AccountEndpointsTestServer _server = null!;

    public async ValueTask InitializeAsync() =>
        _server = await AccountEndpointsTestServer.CreateAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync() =>
        await _server.DisposeAsync().ConfigureAwait(false);

    // -------------------------------------------------------------------------
    // Registration endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Register_ValidRequest_Returns202()
    {
        IIdentityUser fakeUser = Substitute.For<IIdentityUser>();
        fakeUser.UserId.Returns(AccountEndpointsTestServer.TestUserIdString);

        _server.IdentityProvider
            .CreateUserAsync(Arg.Any<Granit.Identity.Models.IdentityUserCreate>(), Arg.Any<CancellationToken>())
            .Returns(fakeUser);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/api/account/register",
            new AccountRegisterRequest("new@example.com", "StrongP@ss1!", "Jane", "Doe"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await _server.EmailConfirmation.Received(1).SendConfirmationEmailAsync(
            AccountEndpointsTestServer.TestUserIdString,
            "new@example.com",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Register_EmailAlreadyTaken_StillReturns202()
    {
        _server.IdentityProvider
            .CreateUserAsync(Arg.Any<Granit.Identity.Models.IdentityUserCreate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Email is already taken"));

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/api/account/register",
            new AccountRegisterRequest("taken@example.com", "StrongP@ss1!", null, null),
            TestContext.Current.CancellationToken);

        // Anti-enumeration: always 202 even if email is taken
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns422()
    {
        _server.IdentityProvider
            .CreateUserAsync(Arg.Any<Granit.Identity.Models.IdentityUserCreate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Password too weak"));

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/api/account/register",
            new AccountRegisterRequest("user@example.com", "weak", null, null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    // -------------------------------------------------------------------------
    // Password endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ForgotPassword_AlwaysReturns202()
    {
        _server.PasswordResetService
            .RequestResetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/api/account/forgot-password",
            new AccountForgotPasswordRequest("user@example.com"),
            TestContext.Current.CancellationToken);

        // Anti-enumeration: always 202
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task ChangePassword_ValidCredentials_Returns204()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "OldP@ss1!", Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/api/account/change-password",
            new AccountPasswordChangeRequest("OldP@ss1!", "NewP@ss2!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.PasswordManager.Received(1).SetTemporaryPasswordAsync(
            AccountEndpointsTestServer.TestUserIdString,
            "NewP@ss2!",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "WrongP@ss!", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/api/account/change-password",
            new AccountPasswordChangeRequest("WrongP@ss!", "NewP@ss2!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_Returns204()
    {
        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/api/account/reset-password",
            new AccountPasswordResetRequest("user-id-1", "valid-token", "NewP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.PasswordResetService.Received(1).ResetPasswordAsync(
            "user-id-1", "valid-token", "NewP@ss1!", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPassword_InvalidToken_Returns400()
    {
        _server.PasswordResetService
            .ResetPasswordAsync("user-id-1", "bad-token", "NewP@ss1!", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Invalid or expired reset token."));

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/api/account/reset-password",
            new AccountPasswordResetRequest("user-id-1", "bad-token", "NewP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Profile endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetProfile_Authenticated_Returns200WithProfile()
    {
        IIdentityUser fakeUser = Substitute.For<IIdentityUser>();
        fakeUser.Email.Returns("test@example.com");
        fakeUser.FirstName.Returns("Jane");
        fakeUser.LastName.Returns("Doe");

        _server.UserReader
            .GetUserAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(fakeUser);

        _server.TwoFactorService
            .GetStatusAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(new TwoFactorStatus(false, false, 0));

        _server.ExternalLoginService
            .GetLoginsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExternalLoginInfo>());

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/api/account/profile", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountProfileResponse? result = await response.Content
            .ReadFromJsonAsync<AccountProfileResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.UserId.ShouldBe(AccountEndpointsTestServer.TestUserId);
        result.Email.ShouldBe("test@example.com");
        result.FirstName.ShouldBe("Jane");
        result.LastName.ShouldBe("Doe");
        result.EmailConfirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateProfile_ValidRequest_Returns200()
    {
        IIdentityUser updatedUser = Substitute.For<IIdentityUser>();
        updatedUser.Email.Returns("test@example.com");
        updatedUser.FirstName.Returns("Updated");
        updatedUser.LastName.Returns("Name");

        _server.UserReader
            .GetUserAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(updatedUser);

        _server.TwoFactorService
            .GetStatusAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(new TwoFactorStatus(false, false, 0));

        _server.ExternalLoginService
            .GetLoginsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExternalLoginInfo>());

        HttpResponseMessage response = await _server.AuthenticatedClient.PutAsJsonAsync(
            "/api/account/profile",
            new AccountProfileUpdateRequest("Updated", "Name"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountProfileResponse? result = await response.Content
            .ReadFromJsonAsync<AccountProfileResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.FirstName.ShouldBe("Updated");
        result.LastName.ShouldBe("Name");

        await _server.UserWriter.Received(1).UpdateUserAsync(
            AccountEndpointsTestServer.TestUserIdString,
            Arg.Any<Granit.Identity.Models.IdentityUserUpdate>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Two-factor endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetTwoFactorStatus_ReturnsStatus()
    {
        _server.TwoFactorService
            .GetStatusAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(new TwoFactorStatus(true, true, 5));

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/api/account/two-factor", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountTwoFactorStatusResponse? result = await response.Content
            .ReadFromJsonAsync<AccountTwoFactorStatusResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.HasAuthenticatorApp.ShouldBeTrue();
        result.RecoveryCodesLeft.ShouldBe(5);
    }

    [Fact]
    public async Task EnableTwoFactor_ValidCode_Returns200WithRecoveryCodes()
    {
        List<string> recoveryCodes = ["CODE-1", "CODE-2", "CODE-3"];

        _server.TwoFactorService
            .EnableAsync(AccountEndpointsTestServer.TestUserIdString, "123456", Arg.Any<CancellationToken>())
            .Returns(recoveryCodes);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/api/account/two-factor/enable",
            new AccountTwoFactorEnableRequest("123456"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountTwoFactorEnableResponse? result = await response.Content
            .ReadFromJsonAsync<AccountTwoFactorEnableResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.RecoveryCodes.Count.ShouldBe(3);
        result.RecoveryCodes.ShouldContain("CODE-1");
    }

    [Fact]
    public async Task EnableTwoFactor_InvalidCode_Returns400()
    {
        _server.TwoFactorService
            .EnableAsync(AccountEndpointsTestServer.TestUserIdString, "000000", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Invalid TOTP code."));

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/api/account/two-factor/enable",
            new AccountTwoFactorEnableRequest("000000"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DisableTwoFactor_ValidPassword_Returns204()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "MyP@ss1!", Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/api/account/two-factor/disable",
            new AccountTwoFactorDisableRequest("MyP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.TwoFactorService.Received(1).DisableAsync(
            AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableTwoFactor_WrongPassword_Returns400()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "WrongP@ss!", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/api/account/two-factor/disable",
            new AccountTwoFactorDisableRequest("WrongP@ss!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Account deletion endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAccount_ValidPassword_Returns202()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "CorrectP@ss!", Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/api/account/delete",
            new AccountDeleteRequest("CorrectP@ss!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await _server.DeletionService.Received(1).InitiateAsync(
            AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAccount_WrongPassword_Returns400()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "WrongP@ss!", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/api/account/delete",
            new AccountDeleteRequest("WrongP@ss!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Session endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SessionHeartbeat_Authenticated_Returns204()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            "/api/account/session/heartbeat", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.FusionCache.Received(1).SetAsync(
            Arg.Is<string>(k => k.StartsWith("session:", StringComparison.Ordinal)),
            Arg.Any<UserSessionActivity>(),
            Arg.Any<FusionCacheEntryOptions>(),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Email confirmation endpoint
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConfirmEmail_ValidToken_Returns204()
    {
        _server.EmailConfirmation
            .ConfirmAsync("user-id-1", "valid-token", Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/api/account/confirm-email?userId=user-id-1&token=valid-token",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ConfirmEmail_InvalidToken_Returns400()
    {
        _server.EmailConfirmation
            .ConfirmAsync("user-id-1", "bad-token", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/api/account/confirm-email?userId=user-id-1&token=bad-token",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Anonymous access guard
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetProfile_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/api/account/profile", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
