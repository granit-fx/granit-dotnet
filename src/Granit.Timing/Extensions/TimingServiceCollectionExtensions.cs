using Granit.Timing.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Timing.Extensions;

/// <summary>
/// Extensions for registering Timing module services.
/// </summary>
public static class TimingServiceCollectionExtensions
{
    /// <summary>
    /// Adds Timing module services (IClock, ICurrentTimezoneProvider,
    /// ICurrentFirstDayOfWeekProvider, IPeriodResolver, TimeProvider).
    /// </summary>
    public static IServiceCollection AddGranitTiming(
        this IServiceCollection services,
        Action<ClockOptions>? configure = null)
    {
        // TimeProvider.System is the standard .NET provider (thread-safe, stateless)
        services.TryAddSingleton(TimeProvider.System);

        // Singleton + AsyncLocal: the runtime isolates the value per async context
        services.TryAddSingleton<ICurrentTimezoneProvider, CurrentTimezoneProvider>();
        services.TryAddSingleton<ICurrentFirstDayOfWeekProvider, CurrentFirstDayOfWeekProvider>();

        // Singleton because Clock is stateless (TimeProvider.System is thread-safe)
        services.TryAddSingleton<IClock, Clock>();

        // Scoped: resolution depends on the per-request ambient timezone / first-day providers
        services.TryAddScoped<IPeriodResolver, PeriodResolver>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}
