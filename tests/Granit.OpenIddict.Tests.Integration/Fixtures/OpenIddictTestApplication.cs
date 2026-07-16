using System.Diagnostics.Metrics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Granit.Caching.Extensions;
using Granit.Events;
using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Endpoints.Extensions;
using Granit.Identity.Local.EntityFrameworkCore.Internal;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Granit.OpenIddict.Diagnostics;
using Granit.OpenIddict.Endpoints.Extensions;
using Granit.OpenIddict.EntityFrameworkCore.Extensions;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Server.Extensions;
using Granit.OpenIddict.Tests.Integration.Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
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
    internal const string TestPkjwtClientId = "test-app-pkjwt";
    internal const string TestUserEmail = "testuser@example.com";
    internal const string TestUserPassword = "P@ssw0rd!Strong2024";
    internal const string TestIssuer = "http://localhost";

    /// <summary>
    /// EC P-256 key pair generated once for <c>private_key_jwt</c> tests.
    /// The public key is registered with the OIDC application; the private key
    /// is used by tests to sign client assertions.
    /// </summary>
    private static readonly ECDsa PkjwtKeyPair = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>Gets the private key JWK for signing client assertions in tests.</summary>
    internal static string TestPkjwtPrivateKeyJwk { get; } = ExportEcPrivateKeyJwk(PkjwtKeyPair);

    /// <summary>Gets the public key JWK registered on the OIDC application.</summary>
    private static string TestPkjwtPublicKeyJwk { get; } = ExportEcPublicKeyJwk(PkjwtKeyPair);

    public async ValueTask InitializeAsync()
    {
        await _postgres.InitializeAsync();

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Explicit issuer — TestServer has no real URL for OpenIddict to auto-detect
        builder.Configuration["OpenIddict:Issuer"] = TestIssuer;
        // TestServer runs as Production by default; ephemeral signing/encryption keys
        // are forbidden there unless explicitly opted in. Persistent keys are not
        // worth provisioning for an in-memory integration suite.
        builder.Configuration["OpenIddict:AllowEphemeralKeys"] = "true";

        // 1. Register OpenIddict EF Core + Identity, then the server pipeline.
        builder.AddGranitOpenIddictEntityFrameworkCore(
            options => options.UseNpgsql(_postgres.ConnectionString));
        builder.AddGranitOpenIddictServer();

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
        builder.Services.TryAddScoped<IAuthenticatorTwoFactorService>(
            _ => Substitute.For<IAuthenticatorTwoFactorService>());
        builder.Services.TryAddScoped<IEmailTwoFactorService>(
            _ => Substitute.For<IEmailTwoFactorService>());
        builder.Services.TryAddScoped<IExternalLoginService>(
            _ => Substitute.For<IExternalLoginService>());
        builder.Services.TryAddScoped<IPasskeyService>(
            _ => Substitute.For<IPasskeyService>());
        builder.Services.TryAddSingleton<IDistributedEventBus>(
            _ => Substitute.For<IDistributedEventBus>());
        Granit.Settings.Services.ISettingProvider settingProvider =
            Substitute.For<Granit.Settings.Services.ISettingProvider>();
        settingProvider
            .GetOrNullAsync(Granit.Identity.Local.IdentityLocalSettingNames.AllowSelfRegistration, Arg.Any<CancellationToken>())
            .Returns("true");
        builder.Services.TryAddSingleton(settingProvider);
        builder.Services.TryAddSingleton<ICurrentTenant>(
            _ => Substitute.For<ICurrentTenant>());
        builder.Services.AddDistributedMemoryCache();

        // FAPI 2.0 enables DPoP — IDPoPProofValidator depends on IClock + IFusionCache.
        // AddGranitCaching() pulls in AddGranitTiming() transitively.
        builder.Services.AddGranitCaching();

        // 4. Metrics (requires IMeterFactory)
        builder.Services.TryAddSingleton<IMeterFactory>(
            _ => new TestMeterFactory());
        builder.Services.TryAddSingleton<OpenIddictMetrics>();
        builder.Services.TryAddSingleton<Granit.Identity.Local.Diagnostics.IdentityLocalMetrics>();

        // 5. Authorization (required by admin endpoints)
        builder.Services.AddAuthorization();

        _app = builder.Build();

        _app.UseAuthentication();
        _app.UseAuthorization();

        // OIDC protocol handler — OpenIddict validates requests via UseAuthentication(),
        // then passes through to ASP.NET Core for token issuance (client_credentials).
        _app.MapPost("/connect/token", HandleToken);

        _app.MapGranitOpenIddict();
        _app.MapGranitAccount();

        // 6. Create the schema for both isolated contexts (disjoint tables, same database) + seed.
        //    EnsureCreated is all-or-nothing on the database, so the identity context creates the
        //    database + its tables, then the OpenIddict context adds its own tables into it.
        await using AsyncServiceScope scope = _app.Services.CreateAsyncScope();
#pragma warning disable EF1001 // OpenIddictDbContext / IdentityLocalDbContext are internal — accessible via InternalsVisibleTo
        IDbContextFactory<IdentityLocalDbContext> identityFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<IdentityLocalDbContext>>();
        await using (IdentityLocalDbContext identityDb = await identityFactory.CreateDbContextAsync())
        {
            await identityDb.Database.EnsureCreatedAsync();
        }

        IDbContextFactory<OpenIddictDbContext> dbFactory =
            scope.ServiceProvider.GetRequiredService<IDbContextFactory<OpenIddictDbContext>>();
        await using OpenIddictDbContext db = await dbFactory.CreateDbContextAsync();
#pragma warning restore EF1001
        await db.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();

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

    /// <summary>Gets the application's root service provider (for resolving managers/factories in tests).</summary>
    internal IServiceProvider Services =>
        _app?.Services ?? throw new InvalidOperationException("App not initialized.");

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
    private static IResult HandleToken(HttpContext context)
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
        // Seed OIDC application (client_secret)
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

        // Seed OIDC application (private_key_jwt — no client secret, public key registered)
        object? pkjwtExisting = await appManager.FindByClientIdAsync(TestPkjwtClientId);
        if (pkjwtExisting is null)
        {
            var pkjwtDescriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = TestPkjwtClientId,
                DisplayName = "Test Application (private_key_jwt)",
                ClientType = OpenIddictConstants.ClientTypes.Confidential,
            };

            pkjwtDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            pkjwtDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Introspection);
            pkjwtDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
            pkjwtDescriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            pkjwtDescriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.RefreshToken);
            pkjwtDescriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + "openid");

            // Register the public key for client assertion validation
            var publicJwk = new JsonWebKey(TestPkjwtPublicKeyJwk) { Use = JsonWebKeyUseNames.Sig };
            var jwks = new JsonWebKeySet();
            jwks.Keys.Add(publicJwk);
            pkjwtDescriptor.JsonWebKeySet = jwks;

            await appManager.CreateAsync(pkjwtDescriptor);
        }

        // Seed test user
        UserManager<LocalIdentity> userManager =
            services.GetRequiredService<UserManager<LocalIdentity>>();

        LocalIdentity? user = await userManager.FindByEmailAsync(TestUserEmail);
        if (user is null)
        {
            user = new LocalIdentity
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

    private static string ExportEcPrivateKeyJwk(ECDsa ecdsa)
    {
        ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: true);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncode(parameters.Q.X!),
            ["y"] = Base64UrlEncode(parameters.Q.Y!),
            ["d"] = Base64UrlEncode(parameters.D!),
        });
    }

    private static string ExportEcPublicKeyJwk(ECDsa ecdsa)
    {
        ECParameters parameters = ecdsa.ExportParameters(includePrivateParameters: false);
        return JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = "EC",
            ["crv"] = "P-256",
            ["x"] = Base64UrlEncode(parameters.Q.X!),
            ["y"] = Base64UrlEncode(parameters.Q.Y!),
        });
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

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
