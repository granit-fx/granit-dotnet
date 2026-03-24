using Granit.Http.Cookies.Klaro.Extensions;
using Granit.Modularity;

namespace Granit.Http.Cookies.Klaro;

/// <summary>
/// Granit module for the Klaro CMP integration.
/// Depends on <see cref="GranitHttpCookiesModule"/> for the cookie management infrastructure.
/// Registration is done via <see cref="GranitCookiesBuilderExtensions.UseKlaro"/>
/// or automatically through the module system.
/// </summary>
[DependsOn(typeof(GranitHttpCookiesModule))]
public sealed class GranitHttpCookiesKlaroModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitCookiesKlaro();
}
