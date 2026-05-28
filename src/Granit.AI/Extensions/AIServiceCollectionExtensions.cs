using System.Diagnostics.CodeAnalysis;
using Granit.AI.Diagnostics;
using Granit.AI.Exports;
using Granit.AI.Internal;
using Granit.AI.Options;
using Granit.AI.Queries;
using Granit.AI.Tenancy;
using Granit.AI.Workspaces;
using Granit.Authorization;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.QueryEngine.Extensions;
using Granit.Settings.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.AI.Extensions;

/// <summary>
/// Extension methods for registering Granit AI core services.
/// </summary>
/// <remarks>Not dead code — called by host applications to register AI core services.</remarks>
[ExcludeFromCodeCoverage]
public static class AIServiceCollectionExtensions
{
    /// <summary>
    /// Adds Granit AI core services: workspace management, chat client factory, and usage tracking.
    /// </summary>
    /// <remarks>
    /// This registers the provider-agnostic infrastructure. You must also register at least one
    /// provider (e.g. <c>AddGranitAIOpenAI()</c>) for the factories to resolve clients.
    /// Without a persistence adapter (<c>Granit.AI.EntityFrameworkCore</c>), null implementations
    /// are used for workspace storage and usage tracking (graceful degradation).
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAI(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIActivitySource.Name);

        builder.Services
            .AddOptions<GranitAIOptions>()
            .BindConfiguration(GranitAIOptions.SectionName);

        // Workspace infrastructure (Scoped — EF Core persistence overrides with scoped stores)
        builder.Services.TryAddScoped<IAIWorkspaceProvider, DefaultAIWorkspaceProvider>();
        builder.Services.TryAddScoped<IAIWorkspaceStoreReader, NullAIWorkspaceStoreReader>();
        builder.Services.TryAddScoped<IAIWorkspaceStoreWriter, NullAIWorkspaceStoreWriter>();
        builder.Services.TryAddScoped<IAIWorkspaceManager, DefaultAIWorkspaceManager>();
        builder.Services.TryAddScoped<IAIWorkspaceCapabilityResolver, DefaultAIWorkspaceCapabilityResolver>();

        // Factories (Scoped — depend on IAIWorkspaceProvider which may consume scoped stores)
        builder.Services.TryAddScoped<IAIChatClientFactory, DefaultAIChatClientFactory>();
        builder.Services.TryAddScoped<IAIEmbeddingGeneratorFactory, DefaultAIEmbeddingGeneratorFactory>();

        // Metrics
        builder.Services.TryAddSingleton<AIMetrics>();

        // Usage tracking (no-op by default, overridden by EF Core package)
        builder.Services.TryAddScoped<IAIUsageTracker, NullAIUsageTracker>();
        builder.Services.TryAddScoped<IAIUsageRecordFactory, AIUsageRecordFactory>();

        // Quota guard: InMemory by default (no-op when MaxRequestsPerTenantPerHour=0)
        builder.Services
            .AddOptions<Options.AIQuotaOptions>()
            .BindConfiguration(Options.AIQuotaOptions.SectionName);

        builder.Services.TryAddSingleton<IAIQuotaGuard, InMemoryAIQuotaGuard>();

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<AIUsageRecord, AIUsageRecordQueryDefinition>();
        builder.Services.AddExportDefinition<AIUsageRecord, AIUsageRecordExportDefinition>();

        // Wrap the configured ISettingManager so that writes to Granit.AI.* keys require the
        // AI.Credentials.Manage permission (in addition to the standard Settings.*.Manage). See
        // AISettingsCredentialsGuard for the rationale.
        DecorateSettingManagerWithCredentialsGuard(builder.Services);

        return builder;
    }

    private static void DecorateSettingManagerWithCredentialsGuard(IServiceCollection services)
    {
        // Find the latest registration (Granit.Settings registers SettingManager via TryAdd
        // before this method runs).
        ServiceDescriptor? existing = null;
        for (int i = services.Count - 1; i >= 0; i--)
        {
            if (services[i].ServiceType == typeof(ISettingManager))
            {
                existing = services[i];
                services.RemoveAt(i);
                break;
            }
        }

        if (existing is null)
        {
            // Settings was not registered yet; the guard cannot wrap nothing. Leaving early
            // is safe because GranitAIModule depends on GranitSettingsModule and the module
            // initialization order normally guarantees Settings runs first. Tests that bypass
            // the module bootstrap still see the inner manager.
            return;
        }

        Func<IServiceProvider, ISettingManager>? resolveInner = null;
        ServiceLifetime guardLifetime = existing.Lifetime;

        if (existing.ImplementationType is { } implType)
        {
            // Re-register the underlying implementation under its concrete type so the guard
            // can pull it via DI.
            services.Add(new ServiceDescriptor(implType, implType, existing.Lifetime));
            resolveInner = sp => (ISettingManager)sp.GetRequiredService(implType);
        }
        else if (existing.ImplementationFactory is { } factory)
        {
            resolveInner = sp => (ISettingManager)factory(sp);
        }
        else if (existing.ImplementationInstance is ISettingManager instance)
        {
            resolveInner = _ => instance;
            guardLifetime = ServiceLifetime.Singleton;
        }

        if (resolveInner is null)
        {
            return;
        }

        services.Add(BuildGuardDescriptor(resolveInner, guardLifetime));
    }

    private static ServiceDescriptor BuildGuardDescriptor(
        Func<IServiceProvider, ISettingManager> resolveInner,
        ServiceLifetime lifetime) =>
        new(
            typeof(ISettingManager),
            sp => new AISettingsCredentialsGuard(
                resolveInner(sp),
                ResolvePermissionChecker(sp, lifetime)),
            lifetime);

    /// <summary>
    /// Returns an <see cref="IPermissionChecker"/> safe to capture in a constructor whose
    /// owning service has the given <paramref name="guardLifetime"/>. For Singleton owners
    /// the scoped <see cref="IPermissionChecker"/> would be a captive dependency, so we
    /// route through <see cref="ScopedPermissionChecker"/> (a singleton-safe wrapper that
    /// creates a fresh DI scope per check). For Scoped / Transient owners direct
    /// resolution is correct.
    /// </summary>
    private static IPermissionChecker ResolvePermissionChecker(IServiceProvider sp, ServiceLifetime guardLifetime) =>
        guardLifetime == ServiceLifetime.Singleton
            ? new ScopedPermissionChecker(sp.GetRequiredService<IServiceScopeFactory>())
            : sp.GetRequiredService<IPermissionChecker>();
}
