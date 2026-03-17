using Granit.Core.Modularity;
using Granit.Features;
using Granit.Http.ExceptionHandling;
using Granit.RateLimiting.Extensions;
using Granit.Security;

namespace Granit.RateLimiting;

/// <summary>
/// Granit module for per-tenant rate limiting.
/// Registers <see cref="Abstractions.IRateLimitCounterStore"/>, <see cref="Abstractions.IRateLimitQuotaProvider"/>,
/// and all required dependencies from configuration section <c>"RateLimiting"</c>.
/// </summary>
[DependsOn(
    typeof(GranitExceptionHandlingModule),
    typeof(GranitFeaturesModule),
    typeof(GranitSecurityModule))]
public sealed class GranitRateLimitingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitRateLimiting();
}
