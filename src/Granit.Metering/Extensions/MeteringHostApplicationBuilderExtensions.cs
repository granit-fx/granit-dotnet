using Granit.Diagnostics;
using Granit.Metering.Diagnostics;
using Granit.Metering.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Metering.Extensions;

/// <summary>
/// Extension methods for registering the Granit metering infrastructure.
/// </summary>
public static class MeteringHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit metering infrastructure.
    /// </summary>
    public static IHostApplicationBuilder AddGranitMetering(
        this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<MeteringMetrics>();
        GranitActivitySourceRegistry.Register(MeteringActivitySource.Name);

        return builder;
    }
}
