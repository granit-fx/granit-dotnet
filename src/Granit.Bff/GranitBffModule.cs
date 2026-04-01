using Granit.Bff.Diagnostics;
using Granit.Bff.Internal;
using Granit.Bff.Options;
using Granit.Caching;
using Granit.Diagnostics;
using Granit.Modularity;
using Granit.Oidc;
using Granit.Timing;
using Granit.Users;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
    typeof(GranitCachingModule),
    typeof(GranitOidcModule),
    typeof(GranitTimingModule))]
public sealed class GranitBffModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddSingleton<IValidateOptions<GranitBffOptions>, GranitBffOptionsValidator>();
        context.Services.TryAddSingleton<BffMetrics>();
        context.Services.TryAddScoped<IBffTokenStore, DistributedCacheBffTokenStore>();
        context.Services.TryAddSingleton<IBffCsrfTokenGenerator, HmacBffCsrfTokenGenerator>();
        context.Services.TryAddSingleton<ILogoutTokenValidator, LogoutTokenValidator>();

        // Internal loopback handler — routes HTTP calls through the ASP.NET Core pipeline
        // in-memory when the BFF authority is the same process (BFF + OpenIddict self-hosted).
        // Auto-detects via IServer addresses; no-op when authority is a remote server.
        BffLoopbackPipelineCapture pipelineCapture = new();
        context.Services.AddSingleton(pipelineCapture);
        context.Services.AddSingleton<IStartupFilter>(pipelineCapture);
        context.Services.AddTransient<InternalLoopbackHandler>();
        context.Services.AddHttpClient("Granit.Bff")
            .AddHttpMessageHandler<InternalLoopbackHandler>();

        GranitActivitySourceRegistry.Register(BffActivitySource.Name);
    }
}
