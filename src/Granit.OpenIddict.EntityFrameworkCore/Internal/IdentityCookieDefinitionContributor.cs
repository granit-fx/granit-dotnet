using Granit.Http.Cookies;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Contributes ASP.NET Core Identity cookie definitions to the Granit cookie registry.
/// Reads actual cookie names from <see cref="CookieAuthenticationOptions"/> to support
/// application-level overrides via <c>ConfigureApplicationCookie()</c>.
/// </summary>
/// <remarks>
/// Default cookie names are overridden by <see cref="GranitOpenIddictEntityFrameworkCoreModule"/>
/// to avoid leaking the underlying technology stack via cookie names.
/// </remarks>
internal sealed class IdentityCookieDefinitionContributor(
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptions) : ICookieDefinitionContributor
{
    /// <summary>Production cookie name — <c>__Host-</c> prefix for CSRF-hardening (RFC 6265bis §4.1.3.2).</summary>
    internal const string DefaultApplicationCookieName = "__Host-id";

    /// <summary>Production 2FA flow cookie name.</summary>
    internal const string DefaultTwoFactorCookieName = "__Host-id-2fa";

    /// <summary>Production external login correlation cookie name.</summary>
    internal const string DefaultExternalCookieName = "__Host-id-ext";

    /// <summary>Development cookie name — no <c>__Host-</c> prefix (requires HTTPS).</summary>
    internal const string DevApplicationCookieName = ".id";

    /// <summary>Development 2FA flow cookie name.</summary>
    internal const string DevTwoFactorCookieName = ".id-2fa";

    /// <summary>Development external login correlation cookie name.</summary>
    internal const string DevExternalCookieName = ".id-ext";

    /// <inheritdoc/>
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        yield return CreateDefinition(
            IdentityConstants.ApplicationScheme,
            DefaultApplicationCookieName,
            "Identity authentication session.");

        yield return CreateDefinition(
            IdentityConstants.TwoFactorUserIdScheme,
            DefaultTwoFactorCookieName,
            "Temporary cookie for two-factor authentication flow.");

        yield return CreateDefinition(
            IdentityConstants.ExternalScheme,
            DefaultExternalCookieName,
            "Temporary cookie for external login correlation.");
    }

    private CookieDefinition CreateDefinition(string scheme, string fallbackName, string purpose)
    {
        string name = cookieOptions.Get(scheme).Cookie.Name ?? fallbackName;

        return new CookieDefinition(name, CookieCategory.StrictlyNecessary, 1, true, purpose)
        {
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
        };
    }
}
