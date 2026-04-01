using Granit.Http.Cookies;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Granit.OpenIddict.EntityFrameworkCore.Internal;

/// <summary>
/// Contributes ASP.NET Core Identity cookie definitions to the Granit cookie registry.
/// Reads actual cookie names from <see cref="CookieAuthenticationOptions"/> to support
/// application-level overrides via <c>ConfigureApplicationCookie()</c>.
/// </summary>
internal sealed class IdentityCookieDefinitionContributor(
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptions) : ICookieDefinitionContributor
{
    /// <inheritdoc/>
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        yield return CreateDefinition(
            IdentityConstants.ApplicationScheme,
            ".AspNetCore.Identity.Application",
            "ASP.NET Core Identity authentication session.");

        yield return CreateDefinition(
            IdentityConstants.TwoFactorUserIdScheme,
            ".AspNetCore.Identity.TwoFactorUserId",
            "Temporary cookie for two-factor authentication flow.");

        yield return CreateDefinition(
            IdentityConstants.ExternalScheme,
            ".AspNetCore.Identity.ExternalLogin",
            "Temporary cookie for external login correlation.");
    }

    private CookieDefinition CreateDefinition(string scheme, string fallbackName, string purpose)
    {
        string name = cookieOptions.Get(scheme).Cookie.Name ?? fallbackName;

        return new CookieDefinition(name, CookieCategory.StrictlyNecessary, 1, true, purpose)
        {
            IsEssential = true,
        };
    }
}
