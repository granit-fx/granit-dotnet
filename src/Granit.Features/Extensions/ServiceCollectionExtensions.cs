using Granit.Features.Definitions;
using Granit.Features.Events;
using Granit.Features.Internal;
using Granit.Features.ValueProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Features.Extensions;

/// <summary>
/// Extension methods for registering Granit.Features services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Feature Management infrastructure.
    /// </summary>
    /// <remarks>
    /// Registers the following services:
    /// <list type="bullet">
    ///   <item><see cref="IFeatureDefinitionStore"/> (singleton) — aggregates all <see cref="IFeatureDefinitionProvider"/> registrations.</item>
    ///   <item><see cref="IFeatureStoreReader"/> / <see cref="IFeatureStoreWriter"/> (singleton) — defaults to <see cref="InMemoryFeatureStore"/>; replace with EF Core store via <c>Granit.Features.EntityFrameworkCore</c>.</item>
    ///   <item>Value providers: Default, Plan, Tenant (scoped).</item>
    ///   <item><see cref="IFeatureChecker"/> (scoped) — resolves feature values with hybrid cache.</item>
    ///   <item><see cref="IFeatureLimitGuard"/> (scoped) — enforces numeric feature limits.</item>
    /// </list>
    /// <para>
    /// To activate plan-level resolution, the application must additionally register
    /// implementations of <c>IPlanIdProvider</c> and <c>IPlanFeatureStore</c>.
    /// </para>
    /// <para>
    /// To declare features, register a <see cref="IFeatureDefinitionProvider"/>:
    /// <code>services.AddFeatureDefinitions&lt;MyFeatureDefinitionProvider&gt;();</code>
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitFeatures(this IServiceCollection services)
    {
        services.AddSingleton<IFeatureDefinitionStore, FeatureDefinitionStore>();

        // Default in-memory store — register concrete type, then forward both interfaces to same instance.
        services.TryAddSingleton<InMemoryFeatureStore>();
        services.TryAddSingleton<IFeatureStoreReader>(sp => sp.GetRequiredService<InMemoryFeatureStore>());
        services.TryAddSingleton<IFeatureStoreWriter>(sp => sp.GetRequiredService<InMemoryFeatureStore>());

        // Value providers — Scoped so request context (ICurrentTenant) is respected
        services.AddScoped<IFeatureValueProvider, DefaultValueFeatureValueProvider>();
        services.AddScoped<IFeatureValueProvider, PlanFeatureValueProvider>();
        services.AddScoped<IFeatureValueProvider, TenantFeatureValueProvider>();

        // Event publisher (no-op default; replaced by Wolverine-backed publisher when available)
        services.TryAddSingleton<IFeatureEventPublisher, NullFeatureEventPublisher>();

        services.AddScoped<IFeatureChecker, FeatureChecker>();
        services.AddScoped<IFeatureLimitGuard, FeatureLimitGuard>();

        return services;
    }

    /// <summary>
    /// Registers a <see cref="IFeatureDefinitionProvider"/> that declares application features.
    /// </summary>
    /// <typeparam name="TProvider">The concrete provider type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFeatureDefinitions<TProvider>(
        this IServiceCollection services) where TProvider : class, IFeatureDefinitionProvider =>
        services.AddSingleton<IFeatureDefinitionProvider, TProvider>();
}
