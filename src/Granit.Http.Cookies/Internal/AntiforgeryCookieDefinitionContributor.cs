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
    /// <summary>
    /// Default antiforgery cookie name set by <see cref="GranitHttpCookiesModule"/>.
    /// Uses the <c>__Host-</c> prefix for CSRF-hardening (RFC 6265bis §4.1.3.2)
    /// and avoids leaking the underlying technology stack.
    /// </summary>
    internal const string DefaultCookieName = "__Host-xsrf";

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
