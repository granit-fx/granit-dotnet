using Granit.Http.Cookies.Klaro.Options;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cookies.Klaro.Internal;

/// <summary>
/// Contributes the Klaro consent cookie to the Granit cookie registry.
/// The cookie name is read from <see cref="KlaroOptions"/> to support configuration overrides.
/// </summary>
internal sealed class KlaroCookieDefinitionContributor(
    IOptions<KlaroOptions> options) : ICookieDefinitionContributor
{
    /// <inheritdoc/>
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        yield return new CookieDefinition(
            options.Value.CookieName,
            CookieCategory.StrictlyNecessary,
            365,
            false,
            "Klaro CMP consent decisions (set by client-side JavaScript).");
    }
}
