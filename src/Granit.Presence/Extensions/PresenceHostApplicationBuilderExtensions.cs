using Granit.Diagnostics;
using Granit.Presence.Abstractions;
using Granit.Presence.Diagnostics;
using Granit.Presence.Internal;
using Granit.Presence.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Presence.Extensions;

/// <summary>
/// Extension methods for registering Granit.Presence services.
/// </summary>
public static class PresenceHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit presence services: in-memory override store, FusionCache-backed
    /// heartbeat tracker, query service, heartbeat recorder, and override service.
    /// </summary>
    /// <remarks>
    /// Default registrations are suitable for development. For production, add
    /// <c>Granit.Presence.EntityFrameworkCore</c> to replace the in-memory store
    /// with EF Core-backed persistence, and <c>Granit.Caching.StackExchangeRedis</c>
    /// to enable cross-pod heartbeat propagation via the Redis backplane.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitPresence(
        this IHostApplicationBuilder builder,
        Action<PresenceOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        GranitActivitySourceRegistry.Register(PresenceActivitySource.Name);
        builder.Services.TryAddSingleton<PresenceMetrics>();

        builder.Services
            .AddOptions<PresenceOptions>()
            .BindConfiguration(PresenceOptions.SectionName)
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<PresenceOptions>, PresenceOptionsValidator>();

        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        // Default (replaceable) store registration.
        builder.Services.TryAddSingleton<InMemoryPresenceStore>();
        builder.Services.TryAddSingleton<IPresenceStore>(sp => sp.GetRequiredService<InMemoryPresenceStore>());

        // Default tracker uses IFusionCache from Granit.Caching (L1 + optional Redis L2 backplane).
        builder.Services.TryAddSingleton<IPresenceTracker, FusionCachePresenceTracker>();

        // Resource-scoped multi-user awareness rooms. Scoped because it consults the request-scoped
        // ICurrentTenant; FusionCache itself is a singleton so concurrency stays cheap.
        builder.Services.TryAddScoped<IResourcePresenceTracker, FusionCacheResourcePresenceTracker>();

        // Permissive default visibility policy. Multi-tenant apps MUST register a tenant-aware
        // replacement before the endpoints are mapped (otherwise cross-tenant reads succeed).
        builder.Services.TryAddSingleton<IPresenceVisibilityPolicy, AllowAllPresenceVisibilityPolicy>();
        builder.Services.TryAddScoped<IResourcePresenceVisibilityPolicy, AllowAllResourcePresenceVisibilityPolicy>();

        // Query service is concrete + interface-registered so internal consumers can take the concrete type.
        builder.Services.TryAddScoped<PresenceQueryService>();
        builder.Services.TryAddScoped<IPresenceQueryService>(sp => sp.GetRequiredService<PresenceQueryService>());

        builder.Services.TryAddScoped<IPresenceHeartbeatRecorder, PresenceHeartbeatRecorder>();
        builder.Services.TryAddScoped<IPresenceOverrideService, PresenceOverrideService>();
        builder.Services.TryAddScoped<IPresenceEraser, PresenceEraser>();
        builder.Services.TryAddSingleton<IPresenceReadAuditSink, LoggingPresenceReadAuditSink>();

        builder.Services.AddHostedService<PresenceStartupChecks>();

        return builder;
    }
}
