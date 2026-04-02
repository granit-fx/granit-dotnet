using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Contributes the ASP.NET Core antiforgery cookie definition to the Granit cookie registry.
/// Reads the actual cookie name from <see cref="AntiforgeryOptions"/> to support
/// application-level overrides via <c>AddAntiforgery()</c>.
/// </summary>
internal sealed class AntiforgeryCookieDefinitionContributor(
    IOptions<AntiforgeryOptions> options) : ICookieDefinitionContributor
{
    /// <summary>Production cookie name — <c>__Host-</c> prefix for CSRF-hardening.</summary>
    internal const string DefaultCookieName = "__Host-xsrf";

    /// <summary>Development cookie name — no <c>__Host-</c> prefix (requires HTTPS).</summary>
    internal const string DevCookieName = ".xsrf";

    /// <inheritdoc/>
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        string name = options.Value.Cookie.Name ?? DefaultCookieName;

        yield return new CookieDefinition(
            name,
            CookieCategory.StrictlyNecessary,
            1,
            true,
            "CSRF protection token.")
        {
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
            IsEssential = true,
        };
    }
}
