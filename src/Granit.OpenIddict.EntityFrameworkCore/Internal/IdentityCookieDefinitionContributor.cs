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
    /// <summary>Default identity session cookie name (avoids leaking ASP.NET Core).</summary>
    internal const string DefaultApplicationCookieName = ".id";

    /// <summary>Default 2FA flow cookie name.</summary>
    internal const string DefaultTwoFactorCookieName = ".id-2fa";

    /// <summary>Default external login correlation cookie name.</summary>
    internal const string DefaultExternalCookieName = ".id-ext";

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
