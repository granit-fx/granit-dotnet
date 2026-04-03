using Granit.Diagnostics;
using Granit.Scheduling.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Scheduling.Extensions;

/// <summary>
/// Extension methods for registering the Granit scheduling infrastructure.
/// </summary>
public static class SchedulingHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit scheduling infrastructure (provider-agnostic).
    /// </summary>
    /// <remarks>
    /// Registers the <see cref="ScheduledPayloadTypeRegistry"/> (type allowlist),
    /// <see cref="SchedulingMetrics"/>, and the <see cref="SchedulingActivitySource"/>.
    /// No <see cref="IScheduler"/> implementation is registered — add
    /// <c>Granit.Scheduling.Wolverine</c> for durable scheduling.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitScheduling(
        this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(SchedulingActivitySource.Name);

        builder.Services.TryAddSingleton<ScheduledPayloadTypeRegistry>();
        builder.Services.TryAddSingleton<SchedulingMetrics>();

        return builder;
    }
}
