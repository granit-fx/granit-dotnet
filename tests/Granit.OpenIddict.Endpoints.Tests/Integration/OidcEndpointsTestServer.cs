using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentValidation;
using Granit.OpenIddict.Endpoints.Extensions;
using Granit.OpenIddict.Endpoints.Options;
using Granit.OpenIddict.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Endpoints.Tests.Integration;

/// <summary>
/// Test server that boots a minimal ASP.NET Core application with mocked services
/// and the OpenIddict admin endpoints registered via <see cref="OpenIddictEndpointRouteBuilderExtensions.MapGranitOpenIddict"/>.
/// </summary>
internal sealed class OidcEndpointsTestServer : IAsyncDisposable
{
    public static readonly Guid TestUserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private const string AdminRole = "admin";

    private readonly WebApplication _app;

    public HttpClient AuthenticatedClient { get; }
    public HttpClient AnonymousClient { get; }

    public IOpenIddictApplicationManager ApplicationManager { get; }
    public IOpenIddictScopeManager ScopeManager { get; }
    public IOpenIddictAuthorizationManager AuthorizationManager { get; }
    public IOpenIddictTokenManager TokenManager { get; }

    private OidcEndpointsTestServer(
        WebApplication app,
        HttpClient authenticatedClient,
        HttpClient anonymousClient,
        IOpenIddictApplicationManager applicationManager,
        IOpenIddictScopeManager scopeManager,
        IOpenIddictAuthorizationManager authorizationManager,
        IOpenIddictTokenManager tokenManager)
    {
        _app = app;
        AuthenticatedClient = authenticatedClient;
        AnonymousClient = anonymousClient;
        ApplicationManager = applicationManager;
        ScopeManager = scopeManager;
        AuthorizationManager = authorizationManager;
        TokenManager = tokenManager;
    }

    public static async Task<OidcEndpointsTestServer> CreateAsync()
    {
        IOpenIddictApplicationManager applicationManager = Substitute.For<IOpenIddictApplicationManager>();
        IOpenIddictScopeManager scopeManager = Substitute.For<IOpenIddictScopeManager>();
        IOpenIddictAuthorizationManager authorizationManager = Substitute.For<IOpenIddictAuthorizationManager>();
        IOpenIddictTokenManager tokenManager = Substitute.For<IOpenIddictTokenManager>();

        // Default: empty async enumerables
        applicationManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<object>());
        scopeManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<object>());
        authorizationManager.ListAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerable.Empty<object>());

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // Authentication
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        // Authorization policies matching the permission names used by the endpoints
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(OpenIddictPermissions.Applications.Read,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(OpenIddictPermissions.Applications.Manage,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(OpenIddictPermissions.Applications.Rotate,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(OpenIddictPermissions.Scopes.Read,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(OpenIddictPermissions.Scopes.Manage,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(OpenIddictPermissions.Authorizations.Read,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(OpenIddictPermissions.Authorizations.Create,
                policy => policy.RequireRole(AdminRole))
            .AddPolicy(OpenIddictPermissions.Authorizations.Revoke,
                policy => policy.RequireRole(AdminRole));

        // Service mocks
        builder.Services.AddSingleton(applicationManager);
        builder.Services.AddSingleton(scopeManager);
        builder.Services.AddSingleton(authorizationManager);
        builder.Services.AddSingleton(tokenManager);

        // Options
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new OpenIddictEndpointsOptions()));

        // Validators from the OpenIddict.Endpoints assembly
        builder.Services.AddValidatorsFromAssemblyContaining<OpenIddictEndpointsOptions>(
            ServiceLifetime.Singleton, includeInternalTypes: true);

        WebApplication app = builder.Build();
        app.MapGranitOpenIddict();
        await app.StartAsync().ConfigureAwait(false);

        HttpClient authenticatedClient = app.GetTestClient();
        authenticatedClient.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, AdminRole);

        HttpClient anonymousClient = app.GetTestClient();

        return new OidcEndpointsTestServer(
            app, authenticatedClient, anonymousClient,
            applicationManager, scopeManager, authorizationManager, tokenManager);
    }

    public async ValueTask DisposeAsync()
    {
        AuthenticatedClient.Dispose();
        AnonymousClient.Dispose();
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}

/// <summary>
/// Fake authentication handler that authenticates when the <c>X-Test-Roles</c> header
/// is present. The authenticated user has a fixed <c>sub</c> claim set to
/// <see cref="OidcEndpointsTestServer.TestUserId"/>.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out Microsoft.Extensions.Primitives.StringValues rolesHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string[] roles = rolesHeader.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries);
        Claim[] claims =
        [
            new("sub", OidcEndpointsTestServer.TestUserId.ToString()),
            new(ClaimTypes.NameIdentifier, OidcEndpointsTestServer.TestUserId.ToString()),
            new(ClaimTypes.Name, "test-admin"),
            .. roles.Select(r => new Claim(ClaimTypes.Role, r.Trim())),
        ];

        ClaimsIdentity identity = new(claims, SchemeName);
        ClaimsPrincipal principal = new(identity);
        AuthenticationTicket ticket = new(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
