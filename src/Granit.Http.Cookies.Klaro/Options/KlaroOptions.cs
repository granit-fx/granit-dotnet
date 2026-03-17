namespace Granit.Http.Cookies.Klaro.Options;

/// <summary>
/// Configuration options for the Klaro CMP integration.
/// Bound to the <c>Klaro</c> configuration section.
/// </summary>
/// <remarks>
/// Service-to-category mappings are now declared in
/// <c>Cookies:ThirdPartyServices</c> (see <see cref="Granit.Http.Cookies.Options.GranitCookiesOptions"/>)
/// and consumed via <see cref="IThirdPartyServiceRegistry"/>.
/// </remarks>
public sealed class KlaroOptions
{
    /// <summary>Section key in the configuration.</summary>
    public const string SectionName = "Klaro";

    /// <summary>
    /// Name of the cookie where Klaro stores consent decisions.
    /// Default: <c>"klaro"</c>.
    /// </summary>
    public string CookieName { get; set; } = "klaro";
}
