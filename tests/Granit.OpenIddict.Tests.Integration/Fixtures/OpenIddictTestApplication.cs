using System.Diagnostics.Metrics;
using System.Security.Claims;
using Granit.Core.Events;
using Granit.Core.MultiTenancy;
using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Identity.Local.AspNetCore.Internal;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Endpoints.Extensions;
using Granit.OpenIddict.Entities;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Services;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Xunit;

namespace Granit.OpenIddict.Tests.Integration.Fixtures;

/// <summary>
/// Test application factory that configures a real OpenIddict OIDC server with
/// PostgreSQL persistence via Testcontainers.
/// </summary>
public sealed class OpenIddictTestApplication : IAsyncLifetime
{
    private readonly PostgresFixture _postgres = new();
    private WebApplication? _app;

    internal const string TestClientId = "test-app";
    internal const string TestClientSecret = "test-secret-K8s!2024#Strong";
    internal const string TestUserEmail = "testuser@example.com";
    internal const string TestUserPassword = "P@ssw0rd!Strong2024";
    internal const string TestIssuer = "http://localhost";

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Explicit issuer — TestServer has no real URL for OpenIddict to auto-detect
        builder.Configuration["OpenIddict:Issuer"] = TestIssuer;

        // 1. Register OpenIddict EF Core + Server + Identity
        builder.AddGranitOpenIddictEntityFrameworkCore(
            options => options.UseNpgsql(_postgres.ConnectionString));

        // Disable HTTPS requirement — TestServer runs over HTTP in-memory
        builder.Services.AddOpenIddict()
            .AddServer(options => options.UseAspNetCore().DisableTransportSecurityRequirement());

        // 2. Register ASP.NET Identity-backed IIdentityProvider
        builder.Services.AddGranitIdentity();
        builder.Services.AddIdentityProvider<AspNetIdentityProvider>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities,
            AspNetIdentityProviderCapabilities>());

        // 3. Register required services that the endpoints depend on
        builder.Services.TryAddScoped<IEmailConfirmationService>(
            _ => Substitute.For<IEmailConfirmationService>());
        builder.Services.TryAddScoped<IPasswordResetService>(
            _ => Substitute.For<IPasswordResetService>());
        builder.Services.TryAddScoped<IAccountDeletionService>(
            _ => Substitute.For<IAccountDeletionService>());
        builder.Services.TryAddScoped<IImpersonationService>(
            _ => Substitute.For<IImpersonationService>());
        builder.Services.TryAddScoped<ITwoFactorService>(
            _ => Substitute.For<ITwoFactorService>());
        builder.Services.TryAddScoped<IExternalLoginService>(
            _ => Substitute.For<IExternalLoginService>());
        builder.Services.TryAddScoped<IPasskeyService>(
            _ => Substitute.For<IPasskeyService>());
        builder.Services.TryAddSingleton<IDistributedEventBus>(
            _ => Substitute.For<IDistributedEventBus>());
        builder.Services.TryAddSingleton<ICurrentTenant>(
            _ => Substitute.For<ICurrentTenant>());
        builder.Services.AddDistributedMemoryCache();

        // 4. Metrics (requires IMeterFactory)
        builder.Services.TryAddSingleton<IMeterFactory>(
            _ => new TestMeterFactory());
        builder.Services.TryAddSingleton<OpenIddictMetrics>();

        // 5. Authorization (required by admin endpoints)
        builder.Services.AddAuthorization();

        _app = builder.Build();

        _app.UseAuthentication();
        _app.UseAuthorization();

        // OIDC protocol handler — OpenIddict validates requests via UseAuthentication(),
        // then passes through to ASP.NET Core for token issuance (client_credentials).
        _app.MapPost("/connect/token", HandleTokenAsync);

        _app.MapOpenIddictEndpoints();

        // 6. EnsureCreated + seed
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
#pragma warning disable EF1001 // OpenIddictDbContext is internal — accessible via InternalsVisibleTo
        IDbContextFactory<OpenIddictDbContext> dbFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<OpenIddictDbContext>>();
        await using OpenIddictDbContext db = await dbFactory.CreateDbContextAsync();
#pragma warning restore EF1001
        await db.Database.EnsureCreatedAsync();

        await SeedTestDataAsync(scope.ServiceProvider);

        await _app.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>Creates a raw <see cref="HttpClient"/> for the test server.</summary>
    public HttpClient CreateHttpClient() =>
        _app?.GetTestClient() ?? throw new InvalidOperationException("App not initialized.");

    /// <summary>Creates an <see cref="OidcTestClient"/> wrapping the test server's HttpClient.</summary>
    public OidcTestClient CreateOidcClient() => new(CreateHttpClient());

    /// <summary>
    /// Minimal token endpoint handler for integration tests.
    /// OpenIddict validates the OIDC request (via passthrough), then this handler
    /// builds a ClaimsPrincipal and signs in to issue the access token.
    /// </summary>
#pragma warning disable GRAPI001 // Test code — SignIn/Forbid have no TypedResults equivalent
    private static IResult HandleTokenAsync(HttpContext context)
    {
        OpenIddictRequest request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict server request not available.");

        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId!);
            identity.SetScopes(request.GetScopes());
            identity.SetDestinations(static _ => [OpenIddictConstants.Destinations.AccessToken]);

            return Results.SignIn(
                new ClaimsPrincipal(identity),
                authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Results.Forbid(
            authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }
#pragma warning restore GRAPI001

    private static async Task SeedTestDataAsync(IServiceProvider services)
    {
        // Seed OIDC application
        IOpenIddictApplicationManager appManager =
            services.GetRequiredService<IOpenIddictApplicationManager>();

        object? existing = await appManager.FindByClientIdAsync(TestClientId);
        if (existing is null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = TestClientId,
                ClientSecret = TestClientSecret,
                DisplayName = "Test Application",
            };

            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.PushedAuthorization);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Scopes.Profile);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "openid");

            descriptor.RedirectUris.Add(new Uri("http://localhost/callback"));

            await appManager.CreateAsync(descriptor);
        }

        // Seed test user
        UserManager<GranitUser> userManager =
            services.GetRequiredService<UserManager<GranitUser>>();

        GranitUser? user = await userManager.FindByEmailAsync(TestUserEmail);
        if (user is null)
        {
            user = new GranitUser
            {
                UserName = TestUserEmail,
                Email = TestUserEmail,
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "User",
            };

            IdentityResult result = await userManager.CreateAsync(user, TestUserPassword);
            if (!result.Succeeded)
            {
                string errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed test user: {errors}");
            }
        }
    }

    /// <summary>Minimal meter factory for test isolation.</summary>
    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }

            _meters.Clear();
        }
    }
}
