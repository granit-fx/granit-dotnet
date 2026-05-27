using Granit.Features;
using Granit.Modularity;
using Granit.RateLimiting.Extensions;

namespace Granit.RateLimiting;

/// <summary>
/// Granit module for the framework-pure rate limiting core.
/// Registers <see cref="Abstractions.IRateLimitCounterStore"/>, <see cref="Abstractions.IRateLimitQuotaProvider"/>,
/// the <see cref="TenantPartitionedRateLimiter"/>, and all required dependencies from configuration
/// section <c>"RateLimiting"</c>.
/// </summary>
/// <remarks>
/// This module is HTTP-agnostic. The ASP.NET Core endpoint filter (429 + Retry-After) lives in
/// <c>Granit.Http.RateLimiting</c>; the Wolverine message middleware lives in
/// <c>Granit.RateLimiting.Wolverine</c>.
/// </remarks>
[DependsOn(typeof(GranitFeaturesModule))]
public sealed class GranitRateLimitingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitRateLimiting();
}
