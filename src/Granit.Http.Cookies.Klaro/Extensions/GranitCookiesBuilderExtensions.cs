namespace Granit.Http.Cookies.Klaro.Extensions;

/// <summary>
/// Extension methods for <see cref="GranitCookiesBuilder"/> to integrate Klaro as the consent resolver.
/// </summary>
public static class GranitCookiesBuilderExtensions
{
    /// <summary>
    /// Configures the Klaro CMP as the consent resolver.
    /// Registers <see cref="KlaroConsentResolver"/> as <see cref="IConsentResolver"/>
    /// and binds <see cref="Options.KlaroOptions"/> from the <c>Klaro</c> configuration section.
    /// </summary>
    /// <remarks>
    /// Requires a <c>Klaro</c> section in configuration with <c>CookieName</c>.
    /// Third-party service mappings are read from <see cref="IThirdPartyServiceRegistry"/>.
    /// </remarks>
    public static GranitCookiesBuilder UseKlaro(this GranitCookiesBuilder builder)
    {
        builder.Services.AddGranitCookiesKlaro();
        return builder;
    }
}
