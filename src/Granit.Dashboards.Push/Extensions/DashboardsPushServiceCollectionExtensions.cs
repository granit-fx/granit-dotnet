using Granit.Dashboards.Push.Internal;
using Granit.Dashboards.Push.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Dashboards.Push.Extensions;

/// <summary>
/// DI registration for the dashboards push transport. Hosts that load this
/// package gain a working <see cref="IWidgetPushPublisher"/> and the SSE
/// endpoint via <c>MapGranitDashboardsPush</c>. Hosts that don't load it
/// continue to render dashboards over the pull endpoint — the framework
/// degrades <c>Realtime</c> widgets to <c>Dynamic</c> cadence with no runtime
/// breakage (ADR-043 §7).
/// </summary>
public static class DashboardsPushServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory push hub + sequence allocator. Idempotent.
    /// Multi-host deployments DI-replace <see cref="IWidgetPushHub"/> + the
    /// public <see cref="IWidgetPushPublisher"/> after this call to swap in a
    /// distributed adapter (Redis / Wolverine).
    /// </summary>
    public static IServiceCollection AddGranitDashboardsPush(
        this IServiceCollection services,
        Action<DashboardsPushOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DashboardsPushOptions>()
            .Configure(opt =>
            {
                configure?.Invoke(opt);
            })
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton<WidgetPushSequenceAllocator>();
        services.TryAddSingleton<InMemoryWidgetPushHub>();
        services.TryAddSingleton<IWidgetPushHub>(sp => sp.GetRequiredService<InMemoryWidgetPushHub>());
        services.TryAddSingleton<IWidgetPushPublisher>(sp => sp.GetRequiredService<InMemoryWidgetPushHub>());

        return services;
    }
}
