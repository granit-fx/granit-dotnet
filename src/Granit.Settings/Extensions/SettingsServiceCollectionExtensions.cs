using Granit.Diagnostics;
using Granit.Events.Extensions;
using Granit.Settings.Definitions;
using Granit.Settings.Diagnostics;
using Granit.Settings.Options;
using Granit.Settings.Providers;
using Granit.Settings.Services;
using Granit.Settings.Stores;
using Granit.Settings.Values;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Settings.Extensions;

/// <summary>
/// Extensions for configuring Granit.Settings services in the DI container.
/// </summary>
public static class SettingsServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Settings module services: definitions, store, providers and services.
    /// </summary>
    /// <param name="services">DI container.</param>
    /// <param name="configuration">Configuration section "Settings". Optional.</param>
    public static IServiceCollection AddGranitSettings(
        this IServiceCollection services,
        IConfigurationSection? configuration = null)
    {
        if (configuration is not null)
        {
            services.Configure<SettingsOptions>(configuration);
        }

        // Diagnostics
        GranitActivitySourceRegistry.Register(SettingsActivitySource.Name);
        services.TryAddSingleton<SettingsMetrics>();

        // Definition registry (Singleton — loaded once at startup)
        services.TryAddSingleton<SettingDefinitionManager>();

        // Default in-memory store (replaced by EfCoreSettingStore in production).
        // Register concrete type first, then forward both interfaces to the same instance.
        services.TryAddSingleton<InMemorySettingStore>();
        services.TryAddSingleton<ISettingStoreReader>(sp => sp.GetRequiredService<InMemorySettingStore>());
        services.TryAddSingleton<ISettingStoreWriter>(sp => sp.GetRequiredService<InMemorySettingStore>());

        // Providers (Scoped — UserSettingValueProvider depends on ICurrentUserService)
        services.AddScoped<ISettingValueProvider, UserSettingValueProvider>();
        services.AddScoped<ISettingValueProvider, TenantSettingValueProvider>();
        services.AddScoped<ISettingValueProvider, GlobalSettingValueProvider>();
        services.AddScoped<ISettingValueProvider, ConfigurationSettingValueProvider>();
        services.AddScoped<ISettingValueProvider, DefaultValueSettingValueProvider>();

        services.TryAddScoped<SettingValueProviderManager>();

        // Event bus fallback (in-process default if not already registered)
        services.AddGranitEvents();
        services.TryAddSingleton(TimeProvider.System);

        // Application services (Scoped — tenant/user context per request)
        services.TryAddScoped<ISettingProvider, SettingProvider>();
        services.TryAddScoped<ISettingManager, SettingManager>();

        return services;
    }
}
