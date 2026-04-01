using System.Net;
using System.Net.Http.Json;
using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Endpoints.Extensions;
using Granit.Identity.Endpoints.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Endpoints.Tests;

/// <summary>
/// Integration tests for identity provider password endpoints (changed-at, reset email, temporary).
/// </summary>
public sealed class IdentityProviderPasswordEndpointsTests : IAsyncDisposable
{
    private const string AdminRole = "granit-identity-admin";
    private const string Prefix = "/identity/provider";

    private readonly IIdentityPasswordManager _passwordManager = Substitute.For<IIdentityPasswordManager>();
    private readonly IIdentityProviderCapabilities _capabilities = Substitute.For<IIdentityProviderCapabilities>();
    private readonly WebApplication _app;
    private readonly HttpClient _adminClient;

    public IdentityProviderPasswordEndpointsTests()
    {
        _capabilities.ProviderName.Returns("Test");
        _capabilities.SupportsUserCreation.Returns(true);
        _capabilities.SupportsIndividualSessionTermination.Returns(true);
        _capabilities.SupportsNativePasswordResetEmail.Returns(true);
        _capabilities.SupportsGroupHierarchy.Returns(true);
        _capabilities.SupportsCustomAttributes.Returns(true);
        _capabilities.SupportsCredentialVerification.Returns(true);
        _capabilities.MaxCustomAttributes.Returns(50);

        _passwordManager.GetPasswordChangedAtAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero));

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(IdentityPermissions.Users.Read,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Users.Manage,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Roles.Read,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Roles.Manage,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Groups.Read,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Groups.Manage,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Sessions.Read,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Sessions.Manage,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(IdentityPermissions.Passwords.Manage,
                policy => policy.RequireRole(AdminRole));
        builder.Services.AddGranitIdentityEndpoints();
        builder.Services.AddSingleton(_passwordManager);
        builder.Services.AddSingleton(_capabilities);

        _app = builder.Build();
        _app.MapGranitIdentityProvider();
        _app.StartAsync().GetAwaiter().GetResult();

        _adminClient = BuildClient(AdminRole);
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    // -- GET /users/{userId}/password/changed-at --

    [Fact]
    public async Task GetPasswordChangedAt_returns_200_with_date()
    {
        HttpResponseMessage response = await _adminClient.GetAsync(
            $"{Prefix}/users/user-1/password/changed-at", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IdentityPasswordChangedAtResponse? result = await response.Content
            .ReadFromJsonAsync<IdentityPasswordChangedAtResponse>(TestContext.Current.CancellationToken);
        result.ShouldNotBeNull();
        result.ChangedAt.ShouldNotBeNull();
    }

    // -- POST /users/{userId}/password/reset-email --

    [Fact]
    public async Task SendPasswordResetEmail_returns_204()
    {
        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/users/user-1/password/reset-email", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _passwordManager.Received(1).SendPasswordResetEmailAsync("user-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendPasswordResetEmail_unsupported_returns_501()
    {
        _capabilities.SupportsNativePasswordResetEmail.Returns(false);

        HttpResponseMessage response = await _adminClient.PostAsync(
            $"{Prefix}/users/user-1/password/reset-email", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotImplemented);
    }

    // -- POST /users/{userId}/password/temporary --

    [Fact]
    public async Task SetTemporaryPassword_returns_204()
    {
        HttpResponseMessage response = await _adminClient.PostAsJsonAsync(
            $"{Prefix}/users/user-1/password/temporary",
            new { Password = "Temp123!" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _passwordManager.Received(1).SetTemporaryPasswordAsync("user-1", "Temp123!", Arg.Any<CancellationToken>());
    }

    private HttpClient BuildClient(string role)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, role);
        return client;
    }
}
