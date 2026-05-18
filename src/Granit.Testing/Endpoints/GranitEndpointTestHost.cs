using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Testing.Endpoints;

/// <summary>
/// In-process ASP.NET Core host for testing a single Granit endpoint module
/// without bootstrapping a full application. Wires <see cref="TestAuthHandler"/>
/// so tests can drive authenticated vs anonymous flows via the
/// <see cref="TestAuthHandler.RolesHeader"/> header.
/// </summary>
public sealed class GranitEndpointTestHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private GranitEndpointTestHost(WebApplication app)
    {
        _app = app;
    }

    /// <summary>The started application — exposed for advanced scenarios.</summary>
    public WebApplication Application => _app;

    /// <summary>
    /// Builds and starts a host. <paramref name="configureServices"/> registers
    /// the services the module under test depends on (mocks, authorization
    /// policies, options). <paramref name="configureEndpoints"/> wires the
    /// <c>Map*</c> extension method that registers the module's routes.
    /// </summary>
    public static async Task<GranitEndpointTestHost> StartAsync(
        Action<IServiceCollection>? configureServices = null,
        Action<WebApplication>? configureEndpoints = null,
        CancellationToken cancellationToken = default)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddRouting();

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName, _ => { });

        configureServices?.Invoke(builder.Services);

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();

        configureEndpoints?.Invoke(app);

        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        return new GranitEndpointTestHost(app);
    }

    /// <summary>Returns an anonymous client (no auth header).</summary>
    public HttpClient CreateAnonymousClient() => _app.GetTestClient();

    /// <summary>
    /// Returns a client carrying the <see cref="TestAuthHandler.RolesHeader"/>
    /// header. <paramref name="roles"/> are joined with commas.
    /// </summary>
    public HttpClient CreateAuthenticatedClient(params string[] roles)
    {
        HttpClient client = _app.GetTestClient();
        client.DefaultRequestHeaders.Add(
            TestAuthHandler.RolesHeader,
            roles.Length == 0 ? "user" : string.Join(',', roles));
        return client;
    }

    public async ValueTask DisposeAsync()
    {
        await _app.DisposeAsync().ConfigureAwait(false);
    }
}
