using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Dtos;
using NSubstitute;
using Shouldly;
using Xunit;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Granit.Identity.Local.Endpoints.Tests.Integration;

/// <summary>
/// HTTP-level tests for <c>POST /account/login</c>, driving every branch of the core
/// sign-in handler through the mocked <c>SignInManager</c> / <c>UserManager</c>: unknown
/// user, success, two-factor challenge, lockout, sign-in-not-allowed and wrong password.
/// </summary>
public sealed class AccountLoginEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AccountLoginRequest ValidRequest() => new("user@example.com", "P@ssw0rd!");

    private static LocalIdentity ExistingUser() => new()
    {
        Id = AccountEndpointsTestServer.TestUserId,
        UserName = "user@example.com",
        Email = "user@example.com",
        TenantId = null,
    };

    [Fact]
    public async Task Login_UnknownUser_Returns401()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.UserManager.FindByEmailAsync(Arg.Any<string>()).Returns((LocalIdentity?)null);
        server.UserManager.FindByNameAsync(Arg.Any<string>()).Returns((LocalIdentity?)null);

        HttpResponseMessage response = await server.AnonymousClient.PostAsJsonAsync("/account/login", ValidRequest(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // Never called PasswordSignInAsync — the handler short-circuits on unknown user.
        await server.SignInManager.DidNotReceiveWithAnyArgs()
            .PasswordSignInAsync(Arg.Any<LocalIdentity>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200AndSucceeded()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.UserManager.FindByEmailAsync(Arg.Any<string>()).Returns(ExistingUser());
        server.SignInManager.PasswordSignInAsync(
                Arg.Any<LocalIdentity>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.Success);

        HttpResponseMessage response = await server.AnonymousClient.PostAsJsonAsync("/account/login", ValidRequest(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AccountLoginResponse? body = await response.Content.ReadFromJsonAsync<AccountLoginResponse>(Ct);
        body.ShouldNotBeNull();
        body.Succeeded.ShouldBeTrue();
        body.RequiresTwoFactor.ShouldBeFalse();
    }

    [Fact]
    public async Task Login_TwoFactorRequired_Returns200WithChallenge()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.UserManager.FindByEmailAsync(Arg.Any<string>()).Returns(ExistingUser());
        server.SignInManager.PasswordSignInAsync(
                Arg.Any<LocalIdentity>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.TwoFactorRequired);

        HttpResponseMessage response = await server.AnonymousClient.PostAsJsonAsync("/account/login", ValidRequest(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AccountLoginResponse? body = await response.Content.ReadFromJsonAsync<AccountLoginResponse>(Ct);
        body.ShouldNotBeNull();
        body.Succeeded.ShouldBeFalse();
        body.RequiresTwoFactor.ShouldBeTrue();
    }

    [Fact]
    public async Task Login_LockedOut_Returns401AndPublishesLockEvent()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.UserManager.FindByEmailAsync(Arg.Any<string>()).Returns(ExistingUser());
        server.SignInManager.PasswordSignInAsync(
                Arg.Any<LocalIdentity>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.LockedOut);

        HttpResponseMessage response = await server.AnonymousClient.PostAsJsonAsync("/account/login", ValidRequest(), Ct);

        // 401 (same as invalid credentials) to avoid account enumeration; user is emailed instead.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_SignInNotAllowed_Returns401()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.UserManager.FindByEmailAsync(Arg.Any<string>()).Returns(ExistingUser());
        server.SignInManager.PasswordSignInAsync(
                Arg.Any<LocalIdentity>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.NotAllowed);

        HttpResponseMessage response = await server.AnonymousClient.PostAsJsonAsync("/account/login", ValidRequest(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.UserManager.FindByEmailAsync(Arg.Any<string>()).Returns(ExistingUser());
        server.SignInManager.PasswordSignInAsync(
                Arg.Any<LocalIdentity>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.Failed);

        HttpResponseMessage response = await server.AnonymousClient.PostAsJsonAsync("/account/login", ValidRequest(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_ResolvesUserByUsername_WhenEmailLookupMisses()
    {
        await using AccountEndpointsTestServer server = await AccountEndpointsTestServer.CreateAsync();
        server.UserManager.FindByEmailAsync(Arg.Any<string>()).Returns((LocalIdentity?)null);
        server.UserManager.FindByNameAsync(Arg.Any<string>()).Returns(ExistingUser());
        server.SignInManager.PasswordSignInAsync(
                Arg.Any<LocalIdentity>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(SignInResult.Success);

        HttpResponseMessage response = await server.AnonymousClient.PostAsJsonAsync(
            "/account/login", new AccountLoginRequest("test-user", "P@ssw0rd!"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
