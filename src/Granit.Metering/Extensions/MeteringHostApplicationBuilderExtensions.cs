using Granit.Diagnostics;
using Granit.Metering.Diagnostics;
using Granit.Metering.Internal;
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
        builder.Services.Configure<GranitMeteringOptions>(
            builder.Configuration.GetSection("Granit:Metering"));

        builder.Services.TryAddSingleton<MeteringMetrics>();
        builder.Services.TryAddSingleton<IQuotaLimitProvider, UnlimitedQuotaLimitProvider>();
        builder.Services.TryAddScoped<IBillingPeriodProvider, CalendarMonthBillingPeriodProvider>();
        GranitActivitySourceRegistry.Register(MeteringActivitySource.Name);

        return builder;
    }
}
