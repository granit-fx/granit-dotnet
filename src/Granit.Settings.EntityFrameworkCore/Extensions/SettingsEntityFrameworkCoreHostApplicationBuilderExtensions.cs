using Granit.DataExchange.Extensions;
using Granit.QueryEngine.Extensions;
using Granit.Settings.Definitions;
using Granit.Settings.EntityFrameworkCore.Entities;
using Granit.Settings.EntityFrameworkCore.Exports;
using Granit.Settings.EntityFrameworkCore.Internal;
using Granit.Settings.EntityFrameworkCore.Queries;
using Granit.Settings.Values;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Settings.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit settings.
/// </summary>
public static class SettingsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default <c>InMemorySettingStore</c> with <see cref="EfCoreSettingStore{TDbContext}"/>,
    /// persisting setting values in the host application's existing DbContext.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <typeparamref name="TDbContext"/> must implement <see cref="ISettingsDbContext"/>
    /// and call <c>modelBuilder.ConfigureSettingsModule()</c> in <c>OnModelCreating</c>.
    /// </para>
    /// <para>
    /// The <c>AuditedEntityInterceptor</c> from <c>Granit.Persistence</c> must be wired
    /// to <typeparamref name="TDbContext"/> by the host application to ensure the ISO 27001
    /// 3-year audit trail is populated on every write.
    /// </para>
    /// </remarks>
    /// <typeparam name="TDbContext">
    /// The host application's DbContext implementing <see cref="ISettingsDbContext"/>.
    /// </typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitSettingsEfCore<TDbContext>(
        this IHostApplicationBuilder builder)
        where TDbContext : DbContext, ISettingsDbContext
    {
        builder.Services.AddSingleton<EfCoreSettingStore<TDbContext>>(sp =>
            new EfCoreSettingStore<TDbContext>(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<SettingDefinitionManager>()));
        builder.Services.Replace(ServiceDescriptor.Singleton<ISettingStoreReader>(sp =>
            sp.GetRequiredService<EfCoreSettingStore<TDbContext>>()));
        builder.Services.Replace(ServiceDescriptor.Singleton<ISettingStoreWriter>(sp =>
            sp.GetRequiredService<EfCoreSettingStore<TDbContext>>()));

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<SettingRecord, SettingRecordQueryDefinition>();
        builder.Services.AddExportDefinition<SettingRecord, SettingRecordExportDefinition>();

        return builder;
    }
}
