using Granit.Events;
using Granit.Events.Extensions;
using Granit.Features.Diagnostics;
using Granit.Features.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Features.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit feature overrides.
/// </summary>
public static class FeaturesEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit feature value overrides.
    /// </summary>
    /// <remarks>
    /// Replaces the default <c>InMemoryFeatureStore</c> registered by
    /// <c>AddGranitFeatures()</c> with <see cref="EfCoreFeatureStore"/>,
    /// backed by <see cref="FeaturesDbContext"/> (table <c>feature_overrides</c>).
    /// <para>
    /// <see cref="AuditedEntityInterceptor"/> is added automatically when
    /// <c>Granit.Persistence</c> is configured, enabling the ISO 27001 3-year audit trail
    /// (<c>created_at</c>, <c>created_by</c>, <c>modified_at</c>, <c>modified_by</c>).
    /// </para>
    /// <para>
    /// Must be called after <c>AddGranitFeatures()</c>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitFeaturesEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<FeaturesDbContext>(configure);
        builder.Services.AddHostInternalDbContextEnsurer<FeaturesDbContext>();

        // Fallbacks: ensure event bus, TimeProvider, and metrics are available even if
        // AddGranitFeatures() / AddGranitEvents() was not called (e.g. in tests).
        builder.Services.AddGranitEvents();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<FeaturesMetrics>();

        builder.Services.Replace(
            ServiceDescriptor.Scoped<IFeatureStoreReader, EfCoreFeatureStore>());
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IFeatureStoreWriter, EfCoreFeatureStore>());

        return builder;
    }
}
