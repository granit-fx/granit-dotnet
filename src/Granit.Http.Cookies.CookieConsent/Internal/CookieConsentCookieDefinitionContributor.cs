using Granit.Http.Cookies.CookieConsent.Options;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cookies.CookieConsent.Internal;

/// <summary>
/// Contributes the @cookieconsent/core consent cookie to the Granit cookie registry.
/// The cookie name is read from <see cref="CookieConsentOptions"/> to support configuration overrides.
/// </summary>
internal sealed class CookieConsentCookieDefinitionContributor(
    IOptions<CookieConsentOptions> options) : ICookieDefinitionContributor
{
    /// <inheritdoc/>
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        yield return new CookieDefinition(
            options.Value.CookieName,
            CookieCategory.StrictlyNecessary,
            365,
            false,
            "@cookieconsent/core CMP consent decisions (set by client-side JavaScript).");
    }
}
