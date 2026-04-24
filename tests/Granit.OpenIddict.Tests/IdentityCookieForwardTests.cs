using Granit.Modularity;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests;

/// <summary>
/// Regression tests for the <see cref="CookieAuthenticationOptions.ForwardDefaultSelector"/>
/// wired onto the <see cref="IdentityConstants.ApplicationScheme"/> cookie by
/// <see cref="GranitOpenIddictModule"/>.
/// </summary>
/// <remarks>
/// <para>
/// Without this forward, a BFF-integrated deployment that serves multiple SPAs from
/// the same backend (host admin + tenant app) routes requests through the cookie
/// scheme whenever the Identity cookie is present — even when a BFF Bearer token
/// has been injected on the request. On localhost the cookie scope ignores the
/// port (RFC 6265) so the Identity cookie is effectively shared across SPAs, and
/// the last-logged-in principal silently overrides the BFF-injected one. The
/// symptom: 403 on host-admin endpoints after a tenant user logs in on the
/// other SPA (and vice-versa).
/// </para>
/// <para>
/// These tests are intentionally end-to-end at the DI level: they boot
/// <see cref="GranitOpenIddictModule"/> against a real <see cref="IServiceCollection"/>,
/// resolve the PostConfigured <see cref="CookieAuthenticationOptions"/> from the
/// container, and invoke the selector. Removing or weakening the registration
/// in the module causes these tests to fail.
/// </para>
/// </remarks>
public sealed class IdentityCookieForwardTests
{
    private const string OpenIddictValidationScheme = "OpenIddict.Validation.AspNetCore";

    [Fact]
    public void ApplicationScheme_ForwardsToBearer_WhenAuthorizationHeaderPresent()
    {
        CookieAuthenticationOptions options = BootstrapAndGetCookieOptions(
            IdentityConstants.ApplicationScheme);

        options.ForwardDefaultSelector.ShouldNotBeNull();

        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Authorization = "Bearer injected-by-bff";

        string? forwarded = options.ForwardDefaultSelector!(httpContext);

        forwarded.ShouldBe(OpenIddictValidationScheme);
    }

    [Fact]
    public void ApplicationScheme_DoesNotForward_WhenAuthorizationHeaderAbsent()
    {
        // Sessionless requests (anonymous GET, OIDC /connect/* endpoints) must
        // keep using the Identity cookie — this is what lets /connect/authorize
        // read the logged-in principal to build the authorization code.
        CookieAuthenticationOptions options = BootstrapAndGetCookieOptions(
            IdentityConstants.ApplicationScheme);

        DefaultHttpContext httpContext = new();

        string? forwarded = options.ForwardDefaultSelector!(httpContext);

        forwarded.ShouldBeNull();
    }

    [Fact]
    public void ApplicationScheme_ForwardsToBearer_ForAnyAuthorizationValue()
    {
        // The selector keys on the PRESENCE of the Authorization header, not its
        // value — a malformed or empty header still triggers the forward, and
        // OpenIddict validation rejects it cleanly as an unauthenticated request.
        // This matters because a selector that parsed the value would risk
        // falling back to cookie-auth on malformed Bearer headers and reviving
        // the cross-frontend leak this forward was added to prevent.
        CookieAuthenticationOptions options = BootstrapAndGetCookieOptions(
            IdentityConstants.ApplicationScheme);

        DefaultHttpContext httpContext = new();
        httpContext.Request.Headers.Authorization = string.Empty;

        string? forwarded = options.ForwardDefaultSelector!(httpContext);

        forwarded.ShouldBe(OpenIddictValidationScheme);
    }

    [Theory]
    // IdentityConstants.* are static readonly strings, not consts — inline
    // the literal values. Cross-checked against the fields in IdentityConstants.
    [InlineData("Identity.TwoFactorUserId")]
    [InlineData("Identity.External")]
    public void NonApplicationIdentityCookies_DoNotForward(string scheme)
    {
        // The forward contract is scoped to the long-lived Application cookie.
        // The 2FA and External cookies are short-lived transport for a specific
        // sub-flow (second-factor challenge / external login callback) and are
        // consumed by handlers that expect a cookie-shaped principal. Forwarding
        // them to Bearer validation would break those flows.
        CookieAuthenticationOptions options = BootstrapAndGetCookieOptions(scheme);

        options.ForwardDefaultSelector.ShouldBeNull();
    }

    private static CookieAuthenticationOptions BootstrapAndGetCookieOptions(string scheme)
    {
        HostApplicationBuilder builder = new();
        ServiceConfigurationContext context = new(
            builder.Services, builder.Configuration, builder);

        new GranitOpenIddictModule().ConfigureServices(context);

        using ServiceProvider sp = builder.Services.BuildServiceProvider();
        IOptionsMonitor<CookieAuthenticationOptions> monitor =
            sp.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        return monitor.Get(scheme);
    }
}
