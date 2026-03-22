using Granit.Bff.Diagnostics;
using Granit.Bff.Internal;
using Granit.Core.Diagnostics;
using Granit.Core.Modularity;
using Granit.Security;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Bff;

/// <summary>
/// Granit module for BFF (Backend For Frontend) security proxy abstractions.
/// </summary>
/// <remarks>
/// Provides <see cref="IBffTokenStore"/> (distributed cache-backed session storage),
/// <see cref="IBffCsrfTokenGenerator"/> (HMAC-SHA256 anti-forgery), and
/// <see cref="Options.GranitBffOptions"/> configuration.
/// Use <c>Granit.Bff.Endpoints</c> for login/logout/user HTTP endpoints and
/// <c>Granit.Bff.Yarp</c> for reverse proxy token injection.
/// </remarks>
[DependsOn(
    typeof(GranitSecurityModule),
    typeof(GranitTimingModule))]
public sealed class GranitBffModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<BffMetrics>();
        context.Services.TryAddScoped<IBffTokenStore, DistributedCacheBffTokenStore>();
        context.Services.TryAddSingleton<IBffCsrfTokenGenerator, HmacBffCsrfTokenGenerator>();

        GranitActivitySourceRegistry.Register(BffActivitySource.Name);
    }
}
