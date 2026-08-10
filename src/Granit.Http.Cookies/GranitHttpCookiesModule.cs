using Granit.Auditing;
using Granit.DataExchange;
using Granit.Http.Cookies.Extensions;
using Granit.Http.Cookies.Internal;
using Granit.Modularity;
using Granit.QueryEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Http.Cookies;

/// <summary>
/// Granit module for GDPR-compliant cookie management.
/// Ensures the cookie infrastructure (registry, manager, consent resolver) is available
/// even if the application host does not call <c>AddGranitCookies()</c> explicitly.
/// </summary>
/// <remarks>
/// Naming: sibling artifacts (<c>GranitCookiesOptions</c>, <c>AddGranitCookies</c>,
/// <c>CookiesLocalizationResource</c>) deliberately drop the <c>Http</c> segment —
/// "Cookies" is unambiguous within the framework and the shorter names predate the
/// package split; only the module class carries the full package name. Kept as-is:
/// consistency-over-churn (§3e audit note, story #2999).
/// </remarks>
[DependsOn(
    typeof(GranitAuditingAbstractionsModule),
    typeof(GranitDataExchangeAbstractionsModule),
    typeof(GranitQueryEngineAbstractionsModule))]
public sealed class GranitHttpCookiesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitCookies(_ => { });

        // Override the default antiforgery cookie name to avoid leaking the technology stack.
        // Production: __Host- prefix (Secure + Path=/ + no Domain, RFC 6265bis §4.1.3.2).
        // Development: simple name without __Host- (requires HTTPS, incompatible with HTTP dev).
        bool isDevelopment = context.Builder.Environment.IsDevelopment();

        context.Services.AddAntiforgery(options =>
        {
            options.Cookie.Name = isDevelopment
                ? AntiforgeryCookieDefinitionContributor.DevCookieName
                : AntiforgeryCookieDefinitionContributor.DefaultCookieName;
            options.Cookie.SecurePolicy = isDevelopment
                ? Microsoft.AspNetCore.Http.CookieSecurePolicy.SameAsRequest
                : Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
        });

        context.Services.AddSingleton<ICookieDefinitionContributor, AntiforgeryCookieDefinitionContributor>();
    }
}
