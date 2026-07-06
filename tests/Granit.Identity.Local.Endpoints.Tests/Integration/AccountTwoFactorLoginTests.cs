using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Granit.Identity.Local.Endpoints.Tests.Integration;

/// <summary>
/// HTTP-level tests for <c>POST /account/login/two-factor</c>, covering every second-factor
/// method (authenticator / recovery code / email OTP) and outcome (success, lockout, invalid).
/// </summary>
public sealed class AccountTwoFactorLoginTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static LocalIdentity TwoFactorUser() => new()
    {
        Id = AccountEndpointsTestServer.TestUserId,
        UserName = "user@example.com",
        Email = "user@example.com",
    };

    private static Task<HttpResponseMessage> PostAsync(AccountEndpointsTestServer server, AccountTwoFactorLoginRequest req) =>
        server.AnonymousClient.PostAsJsonAsync("/account/login/two-factor", req, Ct);

    [Fact]
    public async Task TwoFactor_AuthenticatorValidCode_Returns200()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.SignInManager.TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.Success);

        HttpResponseMessage response = await PostAsync(server, new AccountTwoFactorLoginRequest("123456"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AccountLoginResponse? body = await response.Content.ReadFromJsonAsync<AccountLoginResponse>(Ct);
        body!.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task TwoFactor_AuthenticatorInvalidCode_Returns401()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.SignInManager.TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.Failed);

        HttpResponseMessage response = await PostAsync(server, new AccountTwoFactorLoginRequest("000000"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TwoFactor_LockedOut_Returns401()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.SignInManager.TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.LockedOut);
        server.SignInManager.GetTwoFactorAuthenticationUserAsync().Returns(TwoFactorUser());

        HttpResponseMessage response = await PostAsync(server, new AccountTwoFactorLoginRequest("123456"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TwoFactor_RecoveryCode_StripsFormattingAndSignsIn()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.SignInManager.TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(SignInResult.Success);

        // Code supplied with a dash — the handler must strip it before validating.
        HttpResponseMessage response = await PostAsync(server,
            new AccountTwoFactorLoginRequest("ABCD-1234", TwoFactorMethod.RecoveryCode));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await server.SignInManager.Received(1).TwoFactorRecoveryCodeSignInAsync("ABCD1234");
    }

    [Fact]
    public async Task TwoFactor_RecoveryCodeWithRememberMe_ReSignsPersistent()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.SignInManager.TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(SignInResult.Success);
        server.UserManager.GetUserAsync(Arg.Any<System.Security.Claims.ClaimsPrincipal>())
            .Returns(TwoFactorUser());

        HttpResponseMessage response = await PostAsync(server,
            new AccountTwoFactorLoginRequest("ABCD1234", TwoFactorMethod.RecoveryCode, RememberMe: true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await server.SignInManager.Received(1).SignInAsync(Arg.Any<LocalIdentity>(), isPersistent: true);
    }

    [Fact]
    public async Task TwoFactor_EmailMethodEnabled_Returns200()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.SignInManager.GetTwoFactorAuthenticationUserAsync().Returns(TwoFactorUser());
        server.EmailTwoFactorService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        server.SignInManager.TwoFactorSignInAsync(
                TokenOptions.DefaultEmailProvider, Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.Success);

        HttpResponseMessage response = await PostAsync(server,
            new AccountTwoFactorLoginRequest("123456", TwoFactorMethod.Email));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task TwoFactor_EmailMethodNotEnrolled_Returns401()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.SignInManager.GetTwoFactorAuthenticationUserAsync().Returns(TwoFactorUser());
        // Email 2FA not enrolled → the handler must NOT accept an email token (would otherwise
        // let email access bypass a configured authenticator).
        server.EmailTwoFactorService.IsEnabledAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        HttpResponseMessage response = await PostAsync(server,
            new AccountTwoFactorLoginRequest("123456", TwoFactorMethod.Email));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        await server.SignInManager.DidNotReceiveWithAnyArgs()
            .TwoFactorSignInAsync(default!, default!, default, default);
    }
}
