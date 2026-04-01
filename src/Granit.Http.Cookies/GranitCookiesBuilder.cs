using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.Cookies;

/// <summary>
/// Builder for configuring the Granit.Http.Cookies module.
/// Used within <c>AddGranitCookies()</c> to register cookies and the consent resolver.
/// </summary>
public sealed class GranitCookiesBuilder(IServiceCollection services)
{
    /// <summary>The underlying service collection.</summary>
    internal IServiceCollection Services { get; } = services;

    /// <summary>Cookie definitions to register at startup.</summary>
    internal List<CookieDefinition> CookieDefinitions { get; } = [];

    /// <summary>
    /// Registers a cookie definition in the registry.
    /// </summary>
    public GranitCookiesBuilder RegisterCookie(CookieDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CookieDefinitions.Add(definition);
        return this;
    }

    /// <summary>
    /// Registers the ASP.NET Core session cookie in the RGPD registry.
    /// Call this when the application uses <c>AddSession()</c> / <c>UseSession()</c>.
    /// The cookie name is read from <see cref="Microsoft.AspNetCore.Builder.SessionOptions"/>
    /// at resolution time, defaulting to <c>.AspNetCore.Session</c>.
    /// </summary>
    public GranitCookiesBuilder RegisterSessionCookie()
    {
        Services.AddSingleton<ICookieDefinitionContributor, Internal.SessionCookieDefinitionContributor>();
        return this;
    }

    /// <summary>
    /// Registers the consent resolver implementation.
    /// Applications must call this to provide their CMP integration (Axeptio, Cookiebot, etc.).
    /// </summary>
    public GranitCookiesBuilder UseConsentResolver<TResolver>()
        where TResolver : class, IConsentResolver
    {
        Services.AddScoped<IConsentResolver, TResolver>();
        return this;
    }
}
