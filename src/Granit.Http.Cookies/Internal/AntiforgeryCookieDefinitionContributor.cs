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
    private const string FallbackName = ".AspNetCore.Antiforgery";

    /// <inheritdoc/>
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        string name = options.Value.Cookie.Name ?? FallbackName;

        yield return new CookieDefinition(
            name,
            CookieCategory.StrictlyNecessary,
            1,
            true,
            "CSRF protection token (ASP.NET Core antiforgery).")
        {
            IsEssential = true,
        };
    }
}
