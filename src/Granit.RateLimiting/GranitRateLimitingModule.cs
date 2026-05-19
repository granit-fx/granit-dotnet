using Granit.Features;
using Granit.Http.ExceptionHandling;
using Granit.Modularity;
using Granit.RateLimiting.Extensions;

namespace Granit.RateLimiting;

/// <summary>
/// Granit module for per-tenant rate limiting.
/// Registers <see cref="Abstractions.IRateLimitCounterStore"/>, <see cref="Abstractions.IRateLimitQuotaProvider"/>,
/// and all required dependencies from configuration section <c>"RateLimiting"</c>.
/// </summary>
[DependsOn(
    typeof(GranitFeaturesModule),
    typeof(GranitHttpExceptionHandlingModule))]
public sealed class GranitRateLimitingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitRateLimiting();
}
