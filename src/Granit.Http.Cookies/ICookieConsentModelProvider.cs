using Microsoft.AspNetCore.Http;

namespace Granit.Http.Cookies;

/// <summary>
/// Provides the consent model applicable for the current request context.
/// The default implementation returns <c>null</c> (no regulation awareness).
/// The bridge package <c>Granit.Privacy.Regulations.Cookies</c> provides a
/// regulation-aware implementation that reads from <c>IPrivacyRegulationResolver</c>.
/// </summary>
public interface ICookieConsentModelProvider
{
    /// <summary>
    /// Returns the consent model for the current tenant/jurisdiction, or <c>null</c>
    /// if no regulation context is available.
    /// </summary>
    Task<ConsentModelInfo?> GetConsentModelAsync(HttpContext httpContext);
}
