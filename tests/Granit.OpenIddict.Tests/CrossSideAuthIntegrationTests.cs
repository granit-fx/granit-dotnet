using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Granit.Modularity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests;

/// <summary>
/// End-to-end integration tests for the Identity cookie → Bearer forward registered
/// by <see cref="GranitOpenIddictModule"/>. These tests exercise the full ASP.NET
/// Core authentication pipeline (not just the selector delegate) to verify that
/// when a request carries both an Identity cookie AND an <c>Authorization</c>
/// header, the Bearer-principal wins — preventing the cross-frontend session
/// takeover scenario that motivated PR #1190.
/// </summary>
/// <remarks>
/// <para>
/// The test host registers two authentication schemes:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// The real cookie handler on <see cref="IdentityConstants.ApplicationScheme"/>,
/// with the module's <c>ForwardDefaultSelector</c> applied via PostConfigure.
/// </description>
/// </item>
/// <item>
/// <description>
/// A stub OpenIddict validation handler registered under the well-known scheme
/// name <c>"OpenIddict.Validation.AspNetCore"</c> that materializes a distinct
/// principal whenever an <c>Authorization</c> header is present.
/// </description>
/// </item>
/// </list>
/// <para>
/// The stub lets us distinguish "cookie won" from "Bearer won" by comparing the
/// resulting principal's name — if the forwarder is regressed and the cookie
/// principal leaks through, the bearer-principal test fails loudly.
/// </para>
/// </remarks>
public sealed class CrossSideAuthIntegrationTests : IAsyncDisposable
{
    private const string BearerScheme = "OpenIddict.Validation.AspNetCore";
    private const string CookieUserName = "host-admin@example.test";
    private const string BearerUserName = "tenant-user@example.test";

    private readonly WebApplication _app;
    private readonly HttpClient _client;
    private string? _cookieValue;

    public CrossSideAuthIntegrationTests()
    {
        // Development environment — the module's PostConfigureIdentityCookie uses the
        // dev cookie name ".id" (no __Host- prefix, no HTTPS requirement). We need
        // this because TestServer runs on HTTP and Secure/__Host- cookies would be
        // suppressed by the browser-equivalent pipeline.
        WebApplicationBuilder builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        // The module registers services like IExternalLoginService that depend on
        // UserManager<GranitUser> and IClock, which we don't wire in this focused
        // fixture. Disable DI validation on build — these services are never
        // resolved by the pipeline under test (we only exercise authentication).
        builder.Host.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = false;
        });

        // Apply the module — this wires the ForwardDefaultSelector onto Identity.Application.
        ServiceConfigurationContext context = new(
            builder.Services, builder.Configuration, builder);
        new GranitOpenIddictModule().ConfigureServices(context);

        builder.Services
            .AddAuthentication(IdentityConstants.ApplicationScheme)
            .AddCookie(IdentityConstants.ApplicationScheme, options =>
            {
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = ctx =>
                    {
                        // Headless mode: never redirect; fail fast so the test sees 401.
                        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    },
                };
            })
            .AddScheme<AuthenticationSchemeOptions, StubBearerHandler>(BearerScheme, _ => { });

        builder.Services.AddAuthorization();

        _app = builder.Build();
        _app.UseAuthentication();
        _app.UseAuthorization();

        _app.MapGet("/whoami", (HttpContext ctx) =>
        {
            string? name = ctx.User.Identity?.Name;
            string? authType = ctx.User.Identity?.AuthenticationType;
            return TypedResults.Ok(new { name, authType });
        }).RequireAuthorization();

        _app.MapPost("/signin-cookie", async (HttpContext ctx) =>
        {
            ClaimsPrincipal principal = new(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, CookieUserName)],
                IdentityConstants.ApplicationScheme));
            await ctx.SignInAsync(IdentityConstants.ApplicationScheme, principal)
                .ConfigureAwait(false);
            return TypedResults.Ok();
        });

        _app.StartAsync().GetAwaiter().GetResult();
        _client = _app.GetTestClient();
    }

    [Fact]
    public async Task CookieOnly_NoAuthHeader_ResolvesToCookiePrincipal()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await AuthenticateCookieAsync(cancellationToken);

        HttpRequestMessage request = new(HttpMethod.Get, "/whoami");
        AttachCookie(request);

        HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(cancellationToken)).ShouldContain(CookieUserName);
    }

    [Fact]
    public async Task CookieAndBearer_BearerHeaderWins()
    {
        // This is the regression case: a logged-in user on the "other" SPA left an
        // Identity cookie on the browser; the BFF then injects its OWN Authorization
        // header when proxying to /api. The forwarder must route to OpenIddict
        // validation so the server sees the BFF-injected principal, not the cookie.
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await AuthenticateCookieAsync(cancellationToken);

        HttpRequestMessage request = new(HttpMethod.Get, "/whoami");
        AttachCookie(request);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "stub-bearer-token");

        HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        body.ShouldContain(BearerUserName);
        body.ShouldNotContain(CookieUserName);
    }

    [Fact]
    public async Task BearerOnly_ResolvesToBearerPrincipal()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        HttpRequestMessage request = new(HttpMethod.Get, "/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "stub-bearer-token");

        HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(cancellationToken)).ShouldContain(BearerUserName);
    }

    [Fact]
    public async Task NoCookieNoBearer_Returns401()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        HttpRequestMessage request = new(HttpMethod.Get, "/whoami");
        HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task AuthenticateCookieAsync(CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await _client.PostAsync("/signin-cookie", content: null, cancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // TestServer returns Set-Cookie headers on the response; capture the value for later reuse.
        // Module's PostConfigureIdentityCookie sets the dev cookie name to ".id".
        if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies))
        {
            _cookieValue = cookies.FirstOrDefault(c => c.StartsWith(".id=", StringComparison.Ordinal))
                ?.Split(';')[0];
        }

        _cookieValue.ShouldNotBeNull("expected Set-Cookie header after /signin-cookie");
    }

    private void AttachCookie(HttpRequestMessage request)
    {
        if (_cookieValue is not null)
        {
            request.Headers.Add("Cookie", _cookieValue);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync().ConfigureAwait(false);
        await _app.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Stub handler masquerading as OpenIddict Bearer validation. Authenticates any
    /// request that carries an <c>Authorization</c> header by materializing a principal
    /// whose name differs from the cookie principal, so tests can tell which scheme
    /// produced the authenticated user.
    /// </summary>
    private sealed class StubBearerHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("Authorization"))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            ClaimsIdentity identity = new(
                [new Claim(ClaimTypes.Name, BearerUserName)],
                BearerScheme);
            AuthenticationTicket ticket = new(
                new ClaimsPrincipal(identity),
                BearerScheme);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
