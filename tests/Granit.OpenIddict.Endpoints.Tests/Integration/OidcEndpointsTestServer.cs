using FluentValidation;
using Granit.MultiTenancy;
using Granit.OpenIddict.Endpoints.Extensions;
using Granit.OpenIddict.Endpoints.Options;
using Granit.OpenIddict.Permissions;
using Granit.Testing.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Endpoints.Tests.Integration;

/// <summary>
/// Test server that boots a minimal ASP.NET Core application with mocked services
/// and the OpenIddict admin endpoints registered via <see cref="OpenIddictEndpointRouteBuilderExtensions.MapGranitOpenIddict"/>.
/// </summary>
internal sealed class OidcEndpointsTestServer : IAsyncDisposable
{
    /// <summary>The permissions every admin OIDC endpoint policy gates on — granted to the authenticated client.</summary>
    private static readonly string[] AdminPermissions =
    [
        OpenIddictPermissions.Applications.Read,
        OpenIddictPermissions.Applications.Manage,
        OpenIddictPermissions.Applications.Rotate,
        OpenIddictPermissions.Scopes.Read,
        OpenIddictPermissions.Scopes.Manage,
        OpenIddictPermissions.Authorizations.Read,
        OpenIddictPermissions.Authorizations.Create,
        OpenIddictPermissions.Authorizations.Revoke,
    ];

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

        // Authentication — shared TestAuthHandler resolves permissions from the X-Test-Permissions header.
        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        // Authorization policies matching the permission names used by the endpoints.
        // Permission-based: each policy is satisfied by a permission claim, never a role.
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(OpenIddictPermissions.Applications.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, OpenIddictPermissions.Applications.Read))
            .AddPolicy(OpenIddictPermissions.Applications.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, OpenIddictPermissions.Applications.Manage))
            .AddPolicy(OpenIddictPermissions.Applications.Rotate,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, OpenIddictPermissions.Applications.Rotate))
            .AddPolicy(OpenIddictPermissions.Scopes.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, OpenIddictPermissions.Scopes.Read))
            .AddPolicy(OpenIddictPermissions.Scopes.Manage,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, OpenIddictPermissions.Scopes.Manage))
            .AddPolicy(OpenIddictPermissions.Authorizations.Read,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, OpenIddictPermissions.Authorizations.Read))
            .AddPolicy(OpenIddictPermissions.Authorizations.Create,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, OpenIddictPermissions.Authorizations.Create))
            .AddPolicy(OpenIddictPermissions.Authorizations.Revoke,
                policy => policy.RequireClaim(TestAuthHandler.PermissionClaimType, OpenIddictPermissions.Authorizations.Revoke));

        // Service mocks
        builder.Services.AddSingleton(applicationManager);
        builder.Services.AddSingleton(scopeManager);
        builder.Services.AddSingleton(authorizationManager);
        builder.Services.AddSingleton(tokenManager);

        // Multi-tenancy default: AddGranit<T>() registers NullTenantContext as ICurrentTenant in a
        // real host. The minimal harness supplies it so tenant-aware admin handlers resolve (host
        // context = no active tenant → applications created global).
        builder.Services.AddSingleton<ICurrentTenant>(NullTenantContext.Instance);

        // Options
        builder.Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new OpenIddictEndpointsOptions()));

        // Validators from the OpenIddict.Endpoints assembly
        builder.Services.AddValidatorsFromAssemblyContaining<OpenIddictEndpointsOptions>(
            ServiceLifetime.Singleton, includeInternalTypes: true);

        WebApplication app = builder.Build();
        app.MapGranitOpenIddict();
        await app.StartAsync().ConfigureAwait(false);

        HttpClient authenticatedClient = app.GetTestClient();
        authenticatedClient.DefaultRequestHeaders.Add(
            TestAuthHandler.PermissionsHeader, string.Join(',', AdminPermissions));

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
