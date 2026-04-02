using Granit.Http.Cookies.Extensions;
using Granit.Http.Cookies.Internal;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Http.Cookies;

/// <summary>
/// Granit module for RGPD-compliant cookie management.
/// Ensures the cookie infrastructure (registry, manager, consent resolver) is available
/// even if the application host does not call <c>AddGranitCookies()</c> explicitly.
/// </summary>
public sealed class GranitHttpCookiesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitCookies(_ => { });

        // Override the default antiforgery cookie name to avoid leaking the technology stack.
        context.Services.AddAntiforgery(options =>
        {
            options.Cookie.Name = AntiforgeryCookieDefinitionContributor.DefaultCookieName;
            options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest;
        });

        context.Services.AddSingleton<ICookieDefinitionContributor, AntiforgeryCookieDefinitionContributor>();
    }
}
