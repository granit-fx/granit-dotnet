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
    /// Registers the ASP.NET Core session cookie in the GDPR registry and overrides
    /// the default cookie name to avoid leaking the technology stack.
    /// Call this when the application uses <c>AddSession()</c> / <c>UseSession()</c>.
    /// </summary>
    /// <param name="isDevelopment">
    /// Pass <c>true</c> in development to use a simple cookie name without <c>__Host-</c> prefix
    /// (which requires HTTPS). In production, the <c>__Host-</c> prefix is used for CSRF-hardening.
    /// </param>
    public GranitCookiesBuilder RegisterSessionCookie(bool isDevelopment = false)
    {
        Services.AddSingleton<ICookieDefinitionContributor, Internal.SessionCookieDefinitionContributor>();

        Services.Configure<Microsoft.AspNetCore.Builder.SessionOptions>(options =>
        {
            options.Cookie.Name = isDevelopment
                ? Internal.SessionCookieDefinitionContributor.DevCookieName
                : Internal.SessionCookieDefinitionContributor.DefaultCookieName;
            options.Cookie.SecurePolicy = isDevelopment
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
            options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
            options.Cookie.HttpOnly = true;
        });

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
