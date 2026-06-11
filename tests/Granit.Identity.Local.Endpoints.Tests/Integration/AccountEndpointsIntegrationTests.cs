using System.Net;
using System.Net.Http.Json;
using Granit.Domain;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Events;
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
            "/account/register",
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
            "/account/register",
            new AccountRegisterRequest("taken@example.com", "StrongP@ss1!", null, null),
            TestContext.Current.CancellationToken);

        // Anti-enumeration: always 202 even if email is taken
        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task Register_SelfRegistrationDisabled_Returns403()
    {
        _server.SettingProvider
            .GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, Arg.Any<CancellationToken>())
            .Returns("false");

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/register",
            new AccountRegisterRequest("new@example.com", "StrongP@ss1!", "Jane", "Doe"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns422()
    {
        _server.IdentityProvider
            .CreateUserAsync(Arg.Any<Granit.Identity.Models.IdentityUserCreate>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Password too weak"));

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/register",
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
            "/account/forgot-password",
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
            "/account/change-password",
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
            "/account/change-password",
            new AccountPasswordChangeRequest("WrongP@ss!", "NewP@ss2!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_ValidToken_Returns204()
    {
        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/reset-password",
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
            "/account/reset-password",
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
            .Returns(new TwoFactorStatus(false, false, false, 0));

        _server.ExternalLoginService
            .GetLoginsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExternalLoginInfo>());

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/account/profile", TestContext.Current.CancellationToken);

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
            .Returns(new TwoFactorStatus(false, false, false, 0));

        _server.ExternalLoginService
            .GetLoginsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExternalLoginInfo>());

        HttpResponseMessage response = await _server.AuthenticatedClient.PutAsJsonAsync(
            "/account/profile",
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
            .Returns(new TwoFactorStatus(true, true, false, 5));

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/account/two-factor", TestContext.Current.CancellationToken);

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

        _server.AuthenticatorTwoFactorService
            .EnableAsync(AccountEndpointsTestServer.TestUserIdString, "123456", Arg.Any<CancellationToken>())
            .Returns(recoveryCodes);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/enable",
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
        _server.AuthenticatorTwoFactorService
            .EnableAsync(AccountEndpointsTestServer.TestUserIdString, "000000", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Invalid TOTP code."));

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/enable",
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
            "/account/two-factor/disable",
            new AccountTwoFactorDisableRequest("MyP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.TwoFactorService.Received(1).DisableAllAsync(
            AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableTwoFactor_WrongPassword_Returns400()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "WrongP@ss!", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/disable",
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
            "/account/delete",
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
            "/account/delete",
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
            "/account/session/heartbeat", null,
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
            "/account/confirm-email?userId=user-id-1&token=valid-token",
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
            "/account/confirm-email?userId=user-id-1&token=bad-token",
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
            "/account/profile", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // -------------------------------------------------------------------------
    // Passkey endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListPasskeys_ReturnsPasskeys()
    {
        List<PasskeyInfo> passkeys =
        [
            new(Guid.NewGuid(), "My YubiKey", DateTimeOffset.UtcNow, null),
            new(Guid.NewGuid(), "Phone", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow),
        ];

        _server.PasskeyService
            .GetPasskeysAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(passkeys);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/account/passkeys", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<PasskeyInfo>? result = await response.Content
            .ReadFromJsonAsync<List<PasskeyInfo>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListPasskeys_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/account/passkeys", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task BeginPasskeyRegistration_ReturnsOptionsJson()
    {
        const string optionsJson = """{"challenge":"abc123"}""";

        _server.PasskeyService
            .BeginRegistrationAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(optionsJson);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            "/account/passkeys/register/begin", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string? result = await response.Content
            .ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);

        result.ShouldBe(optionsJson);
    }

    [Fact]
    public async Task CompletePasskeyRegistration_ValidCredential_Returns201()
    {
        PasskeyInfo createdPasskey = new(Guid.NewGuid(), "My Key", DateTimeOffset.UtcNow, null);

        _server.PasskeyService
            .CompleteRegistrationAsync(
                AccountEndpointsTestServer.TestUserIdString,
                """{"id":"cred123"}""",
                "My Key",
                Arg.Any<CancellationToken>())
            .Returns(createdPasskey);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/passkeys/register/complete",
            new PasskeyRegistrationRequest("""{"id":"cred123"}""", "My Key"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CompletePasskeyRegistration_InvalidCredential_Returns400()
    {
        _server.PasskeyService
            .CompleteRegistrationAsync(
                AccountEndpointsTestServer.TestUserIdString,
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Invalid attestation response."));

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/passkeys/register/complete",
            new PasskeyRegistrationRequest("""{"id":"bad"}""", null),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BeginPasskeyAssertion_ReturnsOptionsJson()
    {
        const string optionsJson = """{"challenge":"xyz789"}""";

        _server.PasskeyService
            .BeginAssertionAsync(Arg.Any<CancellationToken>())
            .Returns(optionsJson);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/account/passkeys/assertion/begin", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string? result = await response.Content
            .ReadFromJsonAsync<string>(TestContext.Current.CancellationToken);

        result.ShouldBe(optionsJson);
    }

    [Fact]
    public async Task CompletePasskeyAssertion_ValidCredential_Returns200()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.PasskeyService
            .CompleteAssertionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GranitPasskeyAssertionResult(true, AccountEndpointsTestServer.TestUserIdString));

        _server.UserManager
            .FindByIdAsync(AccountEndpointsTestServer.TestUserIdString)
            .Returns(fakeUser);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/passkeys/assertion/complete",
            new AccountPasskeyLoginRequest("""{"id":"cred123","response":{}}"""),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CompletePasskeyAssertion_InvalidCredential_Returns401()
    {
        _server.PasskeyService
            .CompleteAssertionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GranitPasskeyAssertionResult(false, null));

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/passkeys/assertion/complete",
            new AccountPasskeyLoginRequest("""{"id":"bad"}"""),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CompletePasskeyAssertion_UserNotFound_Returns401()
    {
        _server.PasskeyService
            .CompleteAssertionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new GranitPasskeyAssertionResult(true, "nonexistent-user-id"));

        _server.UserManager
            .FindByIdAsync("nonexistent-user-id")
            .Returns((LocalIdentity?)null);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/passkeys/assertion/complete",
            new AccountPasskeyLoginRequest("""{"id":"cred123"}"""),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RenamePasskey_Returns204()
    {
        var passkeyId = Guid.NewGuid();

        HttpResponseMessage response = await _server.AuthenticatedClient.PatchAsJsonAsync(
            $"/account/passkeys/{passkeyId}",
            new PasskeyRenameRequest("Renamed Key"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.PasskeyService.Received(1).RenameAsync(
            AccountEndpointsTestServer.TestUserIdString,
            passkeyId,
            "Renamed Key",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePasskey_Returns204()
    {
        var passkeyId = Guid.NewGuid();

        HttpResponseMessage response = await _server.AuthenticatedClient.DeleteAsync(
            $"/account/passkeys/{passkeyId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.PasskeyService.Received(1).DeleteAsync(
            AccountEndpointsTestServer.TestUserIdString,
            passkeyId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeletePasskey_LastCredential_Returns400()
    {
        var passkeyId = Guid.NewGuid();

        _server.PasskeyService
            .DeleteAsync(
                AccountEndpointsTestServer.TestUserIdString,
                passkeyId,
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Cannot delete the last credential."));

        HttpResponseMessage response = await _server.AuthenticatedClient.DeleteAsync(
            $"/account/passkeys/{passkeyId}",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // External login endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ListExternalLogins_ReturnsLogins()
    {
        List<ExternalLoginInfo> logins =
        [
            new("Google", "google-key-123", "Google"),
            new("Microsoft", "ms-key-456", "Microsoft"),
        ];

        _server.ExternalLoginService
            .GetLoginsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(logins);

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/account/external-logins", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        List<ExternalLoginInfo>? result = await response.Content
            .ReadFromJsonAsync<List<ExternalLoginInfo>>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListExternalLogins_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/account/external-logins", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChallengeExternalLogin_ConfiguredProvider_Returns200()
    {
        _server.ExternalProviderRegistry
            .IsProviderConfigured("Google")
            .Returns(true);
        _server.ExternalProviderRegistry
            .IsProviderAvailableAsync("Google", Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/account/external-logins/challenge/Google", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChallengeExternalLogin_ConfiguredButNoSchemeRegistered_Returns500()
    {
        _server.ExternalProviderRegistry
            .IsProviderConfigured("Google")
            .Returns(true);
        _server.ExternalProviderRegistry
            .IsProviderAvailableAsync("Google", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/account/external-logins/challenge/Google", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task ChallengeExternalLogin_UnconfiguredProvider_Returns400()
    {
        _server.ExternalProviderRegistry
            .IsProviderConfigured("NotConfigured")
            .Returns(false);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/account/external-logins/challenge/NotConfigured", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnlinkExternalLogin_ValidProvider_Returns204()
    {
        List<ExternalLoginInfo> logins =
        [
            new("Google", "google-key-123", "Google"),
        ];

        _server.ExternalLoginService
            .GetLoginsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(logins);

        HttpResponseMessage response = await _server.AuthenticatedClient.DeleteAsync(
            "/account/external-logins/Google",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await _server.ExternalLoginService.Received(1).RemoveLoginAsync(
            AccountEndpointsTestServer.TestUserIdString,
            "Google",
            "google-key-123",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnlinkExternalLogin_NotLinked_Returns400()
    {
        _server.ExternalLoginService
            .GetLoginsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ExternalLoginInfo>());

        HttpResponseMessage response = await _server.AuthenticatedClient.DeleteAsync(
            "/account/external-logins/GitHub",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnlinkExternalLogin_LastCredential_Returns400()
    {
        List<ExternalLoginInfo> logins = [new("Google", "google-key-123", "Google")];

        _server.ExternalLoginService
            .GetLoginsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(logins);

        _server.ExternalLoginService
            .RemoveLoginAsync(
                AccountEndpointsTestServer.TestUserIdString,
                "Google",
                "google-key-123",
                Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Cannot remove last login method."));

        HttpResponseMessage response = await _server.AuthenticatedClient.DeleteAsync(
            "/account/external-logins/Google",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExternalLoginCallback_MissingProvider_Returns400()
    {
        // No external auth ticket and no query string — endpoint cannot resolve a provider
        // and rejects with 400. The query-string provider is not trusted, so a missing
        // IdentityConstants.ExternalScheme ticket must fail closed.
        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/account/external-logins/callback",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExternalLoginCallback_QueryStringIgnored_Returns400()
    {
        // The query string is attacker-controlled; the endpoint reads the provider
        // from the IdentityConstants.ExternalScheme ticket only. A request that carries
        // only ?provider=Google with no external ticket must be rejected.
        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/account/external-logins/callback?provider=Google",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _server.ExternalLoginService.DidNotReceive().ProcessCallbackAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginCallback_ValidProvider_Returns200()
    {
        _server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), "Google", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ProcessCallbackResult.Existing(AccountEndpointsTestServer.TestUserId));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, "Google");

        HttpResponseMessage response = await _server.AnonymousClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExternalLoginCallback_DuplicateEmail_Returns409()
    {
        _server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), "Google", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DuplicateEmail: email already in use."));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, "Google");

        HttpResponseMessage response = await _server.AnonymousClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ExternalLoginCallback_UserNotFound_Returns403()
    {
        _server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), "GitHub", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("User not found for external login."));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, "GitHub");

        HttpResponseMessage response = await _server.AnonymousClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExternalLoginCallback_QueryProviderIgnoredWhenSchemeDiffers_UsesScheme()
    {
        // Even when the query string says "GitHub", the actual scheme on the external
        // ticket ("Google") wins. Otherwise an attacker who lands an OAuth callback for
        // one provider could mis-attribute the resulting external login.
        _server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), "Google", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ProcessCallbackResult.Existing(AccountEndpointsTestServer.TestUserId));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback?provider=GitHub");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, "Google");

        HttpResponseMessage response = await _server.AnonymousClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _server.ExternalLoginService.Received(1).ProcessCallbackAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            "Google",
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        await _server.ExternalLoginService.DidNotReceive().ProcessCallbackAsync(
            Arg.Any<System.Security.Claims.ClaimsPrincipal>(),
            "GitHub",
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExternalLoginCallback_ExistingUser_EstablishesSession()
    {
        LocalIdentity user = new() { Id = AccountEndpointsTestServer.TestUserId, Email = "external@example.com" };
        _server.UserManager.FindByIdAsync(AccountEndpointsTestServer.TestUserIdString).Returns(user);
        _server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), "Google", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ProcessCallbackResult.Existing(AccountEndpointsTestServer.TestUserId));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, "Google");

        HttpResponseMessage response = await _server.AnonymousClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExternalLoginCallbackResponse? body = await response.Content
            .ReadFromJsonAsync<ExternalLoginCallbackResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe(ExternalLoginCallbackResponse.StatusCompleted);
        await _server.SignInManager.Received(1).SignInAsync(user, false, null);
    }

    [Fact]
    public async Task ExternalLoginCallback_NeedsProfile_ReturnsTokenAndNoSession()
    {
        _server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), "Google", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ProcessCallbackResult.NeedsProfile(
                new ExternalProfilePrefill("Google", "provider-key-9", Email: null, "Ada", "Lovelace", "ada")));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, "Google");

        HttpResponseMessage response = await _server.AnonymousClient.SendAsync(
            request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        ExternalLoginCallbackResponse? body = await response.Content
            .ReadFromJsonAsync<ExternalLoginCallbackResponse>(TestContext.Current.CancellationToken);
        body.ShouldNotBeNull();
        body.Status.ShouldBe(ExternalLoginCallbackResponse.StatusNeedsProfileCompletion);
        body.UserId.ShouldBeNull();
        body.ContinuationToken.ShouldNotBeNullOrEmpty();
        body.Prefill.ShouldNotBeNull();
        body.Prefill!.FirstName.ShouldBe("Ada");
        // No session established for an incomplete profile.
        await _server.SignInManager.DidNotReceive().SignInAsync(Arg.Any<LocalIdentity>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task CompleteExternalRegistration_ValidToken_CreatesLinksAndSignsIn()
    {
        // Obtain a real continuation token by first driving the needs-profile callback.
        string token = await ObtainContinuationTokenAsync("Google", "pk-complete", email: null);

        _server.UserManager.CreateAsync(Arg.Any<LocalIdentity>())
            .Returns(Microsoft.AspNetCore.Identity.IdentityResult.Success);
        _server.UserManager.AddLoginAsync(Arg.Any<LocalIdentity>(), Arg.Any<Microsoft.AspNetCore.Identity.UserLoginInfo>())
            .Returns(Microsoft.AspNetCore.Identity.IdentityResult.Success);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/external-logins/complete-registration",
            new RegisterExternalRequest(token, "ada@example.com", "Ada", "Lovelace"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _server.UserManager.Received(1).CreateAsync(Arg.Is<LocalIdentity>(u => u.Email == "ada@example.com"));
        await _server.UserManager.Received(1).AddLoginAsync(
            Arg.Any<LocalIdentity>(), Arg.Any<Microsoft.AspNetCore.Identity.UserLoginInfo>());
        // Email was not provider-verified → confirmation must be sent.
        await _server.EmailConfirmation.Received(1).SendConfirmationEmailAsync(
            Arg.Any<string>(), "ada@example.com", Arg.Any<CancellationToken>());
        await _server.SignInManager.Received(1).SignInAsync(Arg.Any<LocalIdentity>(), false, null);
        await _server.EventBus.Received(1).PublishAsync(Arg.Any<UserRegisteredEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CompleteExternalRegistration_InvalidToken_Returns400()
    {
        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/external-logins/complete-registration",
            new RegisterExternalRequest("not-a-valid-token", "ada@example.com"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _server.UserManager.DidNotReceive().CreateAsync(Arg.Any<LocalIdentity>());
    }

    [Fact]
    public async Task CompleteExternalRegistration_SelfRegistrationDisabled_Returns403()
    {
        _server.SettingProvider
            .GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, Arg.Any<CancellationToken>())
            .Returns("false");

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/external-logins/complete-registration",
            new RegisterExternalRequest("any-token", "ada@example.com"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CompleteExternalRegistration_TenantMismatch_Returns403()
    {
        // Mint the token while the request resolves to tenant A...
        var tenantA = Guid.NewGuid();
        _server.CurrentTenant.IsAvailable.Returns(true);
        _server.CurrentTenant.Id.Returns(tenantA);
        string token = await ObtainContinuationTokenAsync("Google", "pk-tenant", email: null);

        // ...then redeem it while the request resolves to tenant B.
        _server.CurrentTenant.Id.Returns(Guid.NewGuid());

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/external-logins/complete-registration",
            new RegisterExternalRequest(token, "ada@example.com"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await _server.UserManager.DidNotReceive().CreateAsync(Arg.Any<LocalIdentity>());
    }

    [Fact]
    public async Task StartExternalLogin_UnknownProvider_Returns400()
    {
        // Registry returns no scheme for an unknown provider → 400 before any challenge.
        HttpResponseMessage response = await _server.AnonymousClient.GetAsync(
            "/account/external-logins/challenge/Unknown/start",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExternalLoginCallback_RedirectMode_Completed_Redirects302WithStatus()
    {
        const string frontendUrl = "http://localhost:5173/auth/external-callback";
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync(frontendUrl);
        LocalIdentity user = new() { Id = AccountEndpointsTestServer.TestUserId, Email = "external@example.com" };
        server.UserManager.FindByIdAsync(AccountEndpointsTestServer.TestUserIdString).Returns(user);
        server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), "Google", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ProcessCallbackResult.Existing(AccountEndpointsTestServer.TestUserId));

        using HttpClient client = server.CreateNonRedirectingAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, "Google");
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(frontendUrl);
        response.Headers.Location.Query.ShouldContain("status=completed");
        await server.SignInManager.Received(1).SignInAsync(user, false, null);
    }

    [Fact]
    public async Task ExternalLoginCallback_RedirectMode_NeedsProfile_Redirects302WithToken()
    {
        const string frontendUrl = "http://localhost:5173/auth/external-callback";
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync(frontendUrl);
        server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), "Google", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ProcessCallbackResult.NeedsProfile(
                new ExternalProfilePrefill("Google", "pk-redir", Email: null, "Ada", "Lovelace", "ada")));

        using HttpClient client = server.CreateNonRedirectingAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, "Google");
        HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldStartWith(frontendUrl);
        response.Headers.Location.Query.ShouldContain("status=needs-profile-completion");
        response.Headers.Location.Query.ShouldContain("token=");
    }

    private async Task<string> ObtainContinuationTokenAsync(string provider, string providerKey, string? email)
    {
        _server.ExternalLoginService
            .ProcessCallbackAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>(), provider, Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ProcessCallbackResult.NeedsProfile(
                new ExternalProfilePrefill(provider, providerKey, email, "Ada", "Lovelace", "ada")));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/account/external-logins/callback");
        request.Headers.Add(TestExternalAuthHandler.ProviderHeader, provider);

        HttpResponseMessage response = await _server.AnonymousClient.SendAsync(
            request, TestContext.Current.CancellationToken);
        ExternalLoginCallbackResponse body = (await response.Content
            .ReadFromJsonAsync<ExternalLoginCallbackResponse>(TestContext.Current.CancellationToken))!;
        return body.ContinuationToken!;
    }

    // -------------------------------------------------------------------------
    // Admin impersonation endpoints
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Impersonate_Success_Returns200()
    {
        var targetUserId = Guid.NewGuid();
        var fakeResult = new ImpersonationResult("access-token", "refresh-token", 3600);

        _server.ImpersonationService
            .ImpersonateAsync(
                targetUserId.ToString(),
                AccountEndpointsTestServer.TestUserIdString,
                "test@example.com",
                Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            $"/admin/users/{targetUserId}/impersonate", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ImpersonationResult? result = await response.Content
            .ReadFromJsonAsync<ImpersonationResult>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-token");
        result.ExpiresIn.ShouldBe(3600);
    }

    [Fact]
    public async Task Impersonate_Anonymous_Returns401()
    {
        var targetUserId = Guid.NewGuid();

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            $"/admin/users/{targetUserId}/impersonate", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Impersonate_ChainImpersonation_Returns403()
    {
        var targetUserId = Guid.NewGuid();

        HttpResponseMessage response = await _server.ImpersonatedClient.PostAsync(
            $"/admin/users/{targetUserId}/impersonate", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // Login error paths
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_InvalidCredentials_Returns401()
    {
        _server.UserManager
            .FindByEmailAsync("unknown@example.com")
            .Returns((LocalIdentity?)null);

        _server.UserManager
            .FindByNameAsync("unknown@example.com")
            .Returns((LocalIdentity?)null);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("unknown@example.com", "SomeP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_LockedOut_Returns401AndPublishesEvent()
    {
        DateTimeOffset lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(5);
        var fakeUser = new LocalIdentity
        {
            Id = AccountEndpointsTestServer.TestUserId,
            Email = "locked@example.com",
            LockoutEnd = lockoutEnd,
        };

        _server.UserManager
            .FindByEmailAsync("locked@example.com")
            .Returns(fakeUser);

        _server.SignInManager
            .PasswordSignInAsync(fakeUser, "MyP@ss1!", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        _server.UserManager
            .GeneratePasswordResetTokenAsync(fakeUser)
            .Returns("reset-token-abc");

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("locked@example.com", "MyP@ss1!"),
            TestContext.Current.CancellationToken);

        // Returns 401 (same as invalid credentials) to prevent account enumeration
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await _server.EventBus.Received(1).PublishAsync(
            Arg.Is<AccountLockedEto>(e =>
                e.UserId == AccountEndpointsTestServer.TestUserId &&
                e.Email == "locked@example.com" &&
                e.ResetToken == "reset-token-abc" &&
                e.LockoutEndUtc == lockoutEnd),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_RequiresTwoFactor_Returns200WithFlag()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.UserManager
            .FindByEmailAsync("2fa@example.com")
            .Returns(fakeUser);

        _server.SignInManager
            .PasswordSignInAsync(fakeUser, "MyP@ss1!", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.TwoFactorRequired);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("2fa@example.com", "MyP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeFalse();
        result.RequiresTwoFactor.ShouldBeTrue();
    }

    [Fact]
    public async Task Login_NotAllowed_Returns401()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.UserManager
            .FindByEmailAsync("unconfirmed@example.com")
            .Returns(fakeUser);

        _server.SignInManager
            .PasswordSignInAsync(fakeUser, "MyP@ss1!", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.NotAllowed);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("unconfirmed@example.com", "MyP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.UserManager
            .FindByEmailAsync("user@example.com")
            .Returns(fakeUser);

        _server.SignInManager
            .PasswordSignInAsync(fakeUser, "WrongP@ss!", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("user@example.com", "WrongP@ss!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_RememberMe_PassesPersistentFlag()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.UserManager
            .FindByEmailAsync("remember@example.com")
            .Returns(fakeUser);

        _server.SignInManager
            .PasswordSignInAsync(fakeUser, "GoodP@ss1!", true, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("remember@example.com", "GoodP@ss1!", RememberMe: true),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Login_Success_Returns200()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.UserManager
            .FindByEmailAsync("success@example.com")
            .Returns(fakeUser);

        _server.SignInManager
            .PasswordSignInAsync(fakeUser, "GoodP@ss1!", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("success@example.com", "GoodP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task Login_WithStaleTenantContext_DisablesFilterAndRealignsTenantToUser()
    {
        // Regression: when the browser carries a live Identity cookie from a prior
        // login on another "side" (e.g. logged in on tenant SPA, now signing in on
        // host SPA), TenantResolutionMiddleware resolves ICurrentTenant from the
        // stale tenant_id claim. Before the fix, the handler only disabled the
        // multi-tenant filter when ICurrentTenant was unavailable — so the lookup
        // ran scoped to the wrong tenant and returned null, producing a 401 for
        // credentials that were in fact valid.
        var staleTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        _server.CurrentTenant.IsAvailable.Returns(true);
        _server.CurrentTenant.Id.Returns(staleTenantId);

        var hostAdmin = new LocalIdentity
        {
            Id = AccountEndpointsTestServer.TestUserId,
            Email = "host-admin@example.com",
            TenantId = null,
        };

        _server.UserManager
            .FindByEmailAsync("host-admin@example.com")
            .Returns(hostAdmin);

        _server.SignInManager
            .PasswordSignInAsync(hostAdmin, "GoodP@ss1!", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("host-admin@example.com", "GoodP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // The filter must have been disabled for the lookup — otherwise the host
        // admin (TenantId == null) would be invisible while the stale tenant is
        // active and the request would have returned 401.
        _server.DataFilter.Received().Disable<IMultiTenant>();

        // And the tenant context must be re-aligned with the resolved user's
        // TenantId (null here) so PasswordSignInAsync and downstream operations
        // no longer run within the stale tenant scope.
        _server.CurrentTenant.Received().Change(null, Arg.Any<string?>());
    }

    // -------------------------------------------------------------------------
    // Two-factor login
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TwoFactorLogin_ValidTotpCode_Returns200()
    {
        _server.SignInManager
            .TwoFactorAuthenticatorSignInAsync("123456", false, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login/two-factor",
            new AccountTwoFactorLoginRequest("123456"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TwoFactorLogin_RecoveryCode_Returns200()
    {
        _server.SignInManager
            .TwoFactorRecoveryCodeSignInAsync("RECOVERY1")
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login/two-factor",
            new AccountTwoFactorLoginRequest("RECOVERY1", Method: TwoFactorMethod.RecoveryCode),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TwoFactorLogin_RememberMe_PassesPersistentFlag()
    {
        _server.SignInManager
            .TwoFactorAuthenticatorSignInAsync("123456", true, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login/two-factor",
            new AccountTwoFactorLoginRequest("123456", RememberMe: true),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TwoFactorLogin_RecoveryCode_RememberMe_ReSignsPersistent()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.SignInManager
            .TwoFactorRecoveryCodeSignInAsync("RECOVERY1")
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        _server.UserManager
            .GetUserAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(fakeUser);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login/two-factor",
            new AccountTwoFactorLoginRequest("RECOVERY1", Method: TwoFactorMethod.RecoveryCode, RememberMe: true),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();

        await _server.SignInManager.Received(1)
            .SignInAsync(fakeUser, isPersistent: true, Arg.Any<string?>());
    }

    [Fact]
    public async Task TwoFactorLogin_InvalidCode_Returns401()
    {
        _server.SignInManager
            .TwoFactorAuthenticatorSignInAsync("000000", false, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login/two-factor",
            new AccountTwoFactorLoginRequest("000000"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TwoFactorLogin_LockedOut_Returns401AndPublishesEvent()
    {
        DateTimeOffset lockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
        var fakeUser = new LocalIdentity
        {
            Id = AccountEndpointsTestServer.TestUserId,
            Email = "2fa-locked@example.com",
            LockoutEnd = lockoutEnd,
        };

        _server.SignInManager
            .TwoFactorAuthenticatorSignInAsync("123456", false, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        _server.SignInManager
            .GetTwoFactorAuthenticationUserAsync()
            .Returns(fakeUser);

        _server.UserManager
            .GeneratePasswordResetTokenAsync(fakeUser)
            .Returns("reset-token-2fa");

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login/two-factor",
            new AccountTwoFactorLoginRequest("123456"),
            TestContext.Current.CancellationToken);

        // Returns 401 (same as invalid code) to prevent account enumeration
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        await _server.EventBus.Received(1).PublishAsync(
            Arg.Is<AccountLockedEto>(e =>
                e.UserId == AccountEndpointsTestServer.TestUserId &&
                e.Email == "2fa-locked@example.com" &&
                e.ResetToken == "reset-token-2fa" &&
                e.LockoutEndUtc == lockoutEnd),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Two-factor additional paths
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAuthenticatorKey_ReturnsKeyAndUri()
    {
        _server.AuthenticatorTwoFactorService
            .GetKeyAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(new AuthenticatorKeyInfo("JBSWY3DPEHPK3PXP", "otpauth://totp/App:test@example.com?secret=JBSWY3DPEHPK3PXP&issuer=App"));

        HttpResponseMessage response = await _server.AuthenticatedClient.GetAsync(
            "/account/two-factor/authenticator-key",
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountAuthenticatorKeyResponse? result = await response.Content
            .ReadFromJsonAsync<AccountAuthenticatorKeyResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.SharedKey.ShouldBe("JBSWY3DPEHPK3PXP");
        result.QrCodeUri.ShouldStartWith("otpauth://");
    }

    [Fact]
    public async Task GenerateRecoveryCodes_ValidPassword_ReturnsCodes()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "MyP@ss1!", Arg.Any<CancellationToken>())
            .Returns(true);

        List<string> codes = ["CODE-A", "CODE-B", "CODE-C", "CODE-D", "CODE-E"];
        _server.TwoFactorService
            .GenerateRecoveryCodesAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(codes);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/recovery-codes",
            new AccountGenerateRecoveryCodesRequest("MyP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AccountRecoveryCodesResponse? result = await response.Content
            .ReadFromJsonAsync<AccountRecoveryCodesResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.RecoveryCodes.Count.ShouldBe(5);
        result.RecoveryCodes.ShouldContain("CODE-A");
    }

    [Fact]
    public async Task GenerateRecoveryCodes_WrongPassword_Returns400()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "WrongP@ss!", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/recovery-codes",
            new AccountGenerateRecoveryCodesRequest("WrongP@ss!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Two-factor — email OTP method
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Login_RequiresTwoFactor_ReturnsAvailableMethods()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.UserManager
            .FindByEmailAsync("2fa@example.com")
            .Returns(fakeUser);
        _server.SignInManager
            .PasswordSignInAsync(fakeUser, "MyP@ss1!", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.TwoFactorRequired);
        _server.TwoFactorService
            .GetAvailableMethodsAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns([TwoFactorMethod.Authenticator, TwoFactorMethod.Email]);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login",
            new AccountLoginRequest("2fa@example.com", "MyP@ss1!"),
            TestContext.Current.CancellationToken);

        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.RequiresTwoFactor.ShouldBeTrue();
        result.TwoFactorMethods.ShouldNotBeNull();
        result.TwoFactorMethods.ShouldContain("Authenticator");
        result.TwoFactorMethods.ShouldContain("Email");
    }

    [Fact]
    public async Task TwoFactorLogin_Email_Enrolled_Returns200()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.SignInManager
            .GetTwoFactorAuthenticationUserAsync()
            .Returns(fakeUser);
        _server.EmailTwoFactorService
            .IsEnabledAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(true);
        _server.SignInManager
            .TwoFactorSignInAsync(Microsoft.AspNetCore.Identity.TokenOptions.DefaultEmailProvider, "123456", false, false)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login/two-factor",
            new AccountTwoFactorLoginRequest("123456", Method: TwoFactorMethod.Email),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AccountLoginResponse? result = await response.Content
            .ReadFromJsonAsync<AccountLoginResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TwoFactorLogin_Email_NotEnrolled_Returns401AndDoesNotVerify()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.SignInManager
            .GetTwoFactorAuthenticationUserAsync()
            .Returns(fakeUser);
        _server.EmailTwoFactorService
            .IsEnabledAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsJsonAsync(
            "/account/login/two-factor",
            new AccountTwoFactorLoginRequest("123456", Method: TwoFactorMethod.Email),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await _server.SignInManager.DidNotReceive().TwoFactorSignInAsync(
            Microsoft.AspNetCore.Identity.TokenOptions.DefaultEmailProvider, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task SendTwoFactorLoginEmailCode_Enrolled_SendsAndReturns204()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.SignInManager
            .GetTwoFactorAuthenticationUserAsync()
            .Returns(fakeUser);
        _server.EmailTwoFactorService
            .IsEnabledAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/account/login/two-factor/send-email", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _server.EmailTwoFactorService.Received(1).SendCodeAsync(
            AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendTwoFactorLoginEmailCode_NotEnrolled_DoesNotSendButReturns204()
    {
        var fakeUser = new LocalIdentity { Id = AccountEndpointsTestServer.TestUserId };

        _server.SignInManager
            .GetTwoFactorAuthenticationUserAsync()
            .Returns(fakeUser);
        _server.EmailTwoFactorService
            .IsEnabledAsync(AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/account/login/two-factor/send-email", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _server.EmailTwoFactorService.DidNotReceive().SendCodeAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendTwoFactorLoginEmailCode_NoSession_Returns400()
    {
        _server.SignInManager
            .GetTwoFactorAuthenticationUserAsync()
            .Returns((LocalIdentity?)null);

        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/account/login/two-factor/send-email", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendTwoFactorEnrollmentEmailCode_Returns204()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            "/account/two-factor/email/send", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _server.EmailTwoFactorService.Received(1).SendCodeAsync(
            AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableTwoFactorEmail_ValidCode_Returns204()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/email/enable",
            new AccountTwoFactorEmailEnableRequest("123456"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _server.EmailTwoFactorService.Received(1).EnableAsync(
            AccountEndpointsTestServer.TestUserIdString, "123456", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableTwoFactorEmail_InvalidCode_Returns400()
    {
        _server.EmailTwoFactorService
            .EnableAsync(AccountEndpointsTestServer.TestUserIdString, "000000", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Invalid email verification code."));

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/email/enable",
            new AccountTwoFactorEmailEnableRequest("000000"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DisableTwoFactorEmail_ValidPassword_Returns204()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "MyP@ss1!", Arg.Any<CancellationToken>())
            .Returns(true);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/email/disable",
            new AccountTwoFactorDisableRequest("MyP@ss1!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _server.EmailTwoFactorService.Received(1).DisableAsync(
            AccountEndpointsTestServer.TestUserIdString, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisableTwoFactorEmail_WrongPassword_Returns400()
    {
        _server.CredentialVerifier
            .VerifyUserCredentialsAsync("test-user", "WrongP@ss!", Arg.Any<CancellationToken>())
            .Returns(false);

        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsJsonAsync(
            "/account/two-factor/email/disable",
            new AccountTwoFactorDisableRequest("WrongP@ss!"),
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _server.EmailTwoFactorService.DidNotReceive().DisableAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Session — back to impersonator
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BackToImpersonator_ImpersonatedSession_Returns200()
    {
        var fakeResult = new ImpersonationResult("admin-access-token", "admin-refresh-token", 3600);

        _server.ImpersonationService
            .BackToImpersonatorAsync(
                AccountEndpointsTestServer.ImpersonatorUserIdString,
                Arg.Any<CancellationToken>())
            .Returns(fakeResult);

        HttpResponseMessage response = await _server.ImpersonatedClient.PostAsync(
            "/account/session/back-to-impersonator", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ImpersonationResult? result = await response.Content
            .ReadFromJsonAsync<ImpersonationResult>(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("admin-access-token");
    }

    [Fact]
    public async Task BackToImpersonator_NotImpersonated_Returns400()
    {
        HttpResponseMessage response = await _server.AuthenticatedClient.PostAsync(
            "/account/session/back-to-impersonator", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BackToImpersonator_Anonymous_Returns401()
    {
        HttpResponseMessage response = await _server.AnonymousClient.PostAsync(
            "/account/session/back-to-impersonator", null,
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
