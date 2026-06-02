namespace Granit.Http.Cookies.CookieConsent.Extensions;

/// <summary>
/// Extension methods for <see cref="GranitCookiesBuilder"/> to integrate @cookieconsent/core as the consent resolver.
/// </summary>
public static class GranitCookiesBuilderExtensions
{
    /// <summary>
    /// Configures @cookieconsent/core as the consent resolver.
    /// Registers <c>CookieConsentConsentResolver</c> as <see cref="IConsentResolver"/>
    /// and binds <see cref="Options.CookieConsentOptions"/> from the <c>Http:Cookies:CookieConsent</c> configuration section.
    /// </summary>
    /// <remarks>
    /// Category names in configuration must match those configured in the front-end
    /// <c>@granit/cookies-cookieconsent</c> adapter.
    /// </remarks>
    public static GranitCookiesBuilder UseCookieConsent(this GranitCookiesBuilder builder)
    {
        builder.Services.AddGranitCookiesCookieConsent();
        return builder;
    }
}
