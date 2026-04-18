using Granit.DataExchange.Extensions;
using Granit.Localization.EntityFrameworkCore.Entities;
using Granit.Localization.EntityFrameworkCore.Exports;
using Granit.Localization.EntityFrameworkCore.Internal;
using Granit.Localization.EntityFrameworkCore.Queries;
using Granit.Localization.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Localization.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit localization overrides.
/// </summary>
public static class LocalizationEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit localization overrides.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers <see cref="EfCoreLocalizationOverrideStore"/> as a keyed Scoped service
    /// (<see cref="CachedLocalizationOverrideStore.RawStoreKey"/>). The Singleton
    /// <see cref="CachedLocalizationOverrideStore"/> — registered by
    /// <c>GranitLocalizationModule</c> — resolves it via
    /// <c>IServiceScopeFactory</c> per DB operation, ensuring ISO 27001 audit compliance
    /// through <see cref="AuditedEntityInterceptor"/> on write operations.
    /// </para>
    /// <para>
    /// Must be called after the module system has been initialized (i.e. after
    /// <c>GranitLocalizationEntityFrameworkCoreModule</c> is loaded).
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitLocalizationEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<LocalizationDbContext>(configure);

        builder.Services.TryAddKeyedScoped<ILocalizationOverrideStoreReader, EfCoreLocalizationOverrideStore>(
            CachedLocalizationOverrideStore.RawStoreKey);
        builder.Services.TryAddKeyedScoped<ILocalizationOverrideStoreWriter, EfCoreLocalizationOverrideStore>(
            CachedLocalizationOverrideStore.RawStoreKey);

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<LocalizationOverride, LocalizationOverrideQueryDefinition>();
        builder.Services.AddExportDefinition<LocalizationOverride, LocalizationOverrideExportDefinition>();

        return builder;
    }
}
