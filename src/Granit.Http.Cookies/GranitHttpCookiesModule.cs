using Granit.Http.Cookies.Extensions;
using Granit.Modularity;

namespace Granit.Http.Cookies;

/// <summary>
/// Granit module for RGPD-compliant cookie management.
/// Ensures the cookie infrastructure (registry, manager, consent resolver) is available
/// even if the application host does not call <c>AddGranitCookies()</c> explicitly.
/// </summary>
public sealed class GranitHttpCookiesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCookies(_ => { });
}
