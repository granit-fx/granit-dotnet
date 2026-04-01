using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Granit.Http.Cookies.Internal;

/// <summary>
/// Contributes the ASP.NET Core session cookie definition to the Granit cookie registry.
/// Reads the actual cookie name from <see cref="SessionOptions"/> to support
/// application-level overrides via <c>AddSession()</c>.
/// </summary>
internal sealed class SessionCookieDefinitionContributor(
    IOptions<SessionOptions> options) : ICookieDefinitionContributor
{
    /// <summary>Default session cookie name (avoids leaking ASP.NET Core).</summary>
    internal const string DefaultCookieName = "__Host-session";

    /// <inheritdoc/>
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        string name = options.Value.Cookie.Name ?? DefaultCookieName;

        yield return new CookieDefinition(
            name,
            CookieCategory.StrictlyNecessary,
            1,
            true,
            "Server-side session identifier.")
        {
            SameSite = SameSiteMode.Strict,
            IsEssential = true,
        };
    }
}
