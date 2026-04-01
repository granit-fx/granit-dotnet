using Microsoft.AspNetCore.Builder;
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
    /// <inheritdoc/>
    public IEnumerable<CookieDefinition> GetCookieDefinitions()
    {
        string name = options.Value.Cookie.Name ?? ".AspNetCore.Session";

        yield return new CookieDefinition(
            name,
            CookieCategory.StrictlyNecessary,
            1,
            true,
            "Server-side session identifier (ASP.NET Core session middleware).")
        {
            IsEssential = true,
        };
    }
}
