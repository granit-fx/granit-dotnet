namespace Granit.Http.Cookies;

/// <summary>
/// Contributes cookie definitions to the <see cref="ICookieRegistry"/> at startup.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface in each Granit module that introduces cookies
/// (either set directly or set by ASP.NET Core middleware the module configures).
/// Contributors are discovered from DI and executed during registry construction.
/// </para>
/// <para>
/// Implementations must be <b>idempotent</b>: the same definitions must be returned
/// on every call. Register implementations as singletons:
/// <code>
/// services.AddSingleton&lt;ICookieDefinitionContributor, IdentityCookieContributor&gt;();
/// </code>
/// </para>
/// </remarks>
public interface ICookieDefinitionContributor
{
    /// <summary>
    /// Returns the cookie definitions this module introduces.
    /// </summary>
    IEnumerable<CookieDefinition> GetCookieDefinitions();
}
