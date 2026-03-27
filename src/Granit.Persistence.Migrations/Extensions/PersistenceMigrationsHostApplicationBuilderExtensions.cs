using System.Threading.Channels;
using Granit.Persistence.Migrations.Internal;
using Granit.Persistence.Migrations.Messages;
using Granit.Persistence.Migrations.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Persistence.Migrations.Extensions;

/// <summary>
/// Extension methods for registering the Granit zero-downtime migrations infrastructure.
/// </summary>
public static class PersistenceMigrationsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit zero-downtime migrations infrastructure.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Registers:
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="MigrationProgressDbContext"/> — system <c>DbContext</c> factory for progress tracking.
    ///     Uses its own connection, never affected by tenant schema switches.
    ///     Committed independently from the tenant data transaction (best-effort progress).
    ///   </item>
    ///   <item>
    ///     <see cref="IMigrationCycleRegistry"/> as a thread-safe singleton.
    ///     Populate it at startup via <see cref="MigrationCycleRegistryExtensions.Register{TContext}"/>.
    ///   </item>
    ///   <item>
    ///     <see cref="ITenantDbIsolator"/> — default no-op implementation, suitable for
    ///     Shared database and Tenant-per-Database topologies. For Tenant-per-Schema,
    ///     register a custom <see cref="ITenantDbIsolator"/> with
    ///     <c>services.AddSingleton&lt;ITenantDbIsolator, MySchemaIsolator&gt;()</c>
    ///     <b>before</b> calling this method.
    ///   </item>
    ///   <item>
    ///     <see cref="ITenantEnumerator"/> — default no-op implementation (empty stream).
    ///     For Tenant-per-Schema or Tenant-per-Database topologies, register a custom
    ///     <see cref="ITenantEnumerator"/> <b>before</b> calling this method.
    ///   </item>
    ///   <item>
    ///     <c>MigrationStartupService</c> — hosted service that resumes pending cycles at startup.
    ///   </item>
    ///   <item>
    ///     <see cref="MigrationStartupOptions"/> — bound from the
    ///     <c>"GranitMigrations"</c> configuration section.
    ///   </item>
    /// </list>
    /// </para>
    /// <para>
    /// <c>RunMigrationBatchHandler</c> is auto-discovered by Wolverine from the assembly;
    /// no explicit handler registration is required.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configureProgressDb">
    /// Provider-specific <see cref="DbContextOptionsBuilder"/> configuration for the system
    /// migration progress database.
    /// Example: <c>opts =&gt; opts.UseNpgsql(connectionString)</c>
    /// </param>
    /// <returns>The builder, for chaining.</returns>
    public static IHostApplicationBuilder AddGranitPersistenceMigrations(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configureProgressDb)
    {
        // System DbContext factory — registered WITHOUT Wolverine EF Core transaction integration
        // so that progress commits are independent from the tenant data transaction.
        builder.Services.AddDbContextFactory<MigrationProgressDbContext>(configureProgressDb);

        // Ensurer — decouples Granit.Persistence.Hosting from the internal MigrationProgressDbContext.
        builder.Services.TryAddSingleton<IMigrationProgressDbEnsurer, MigrationProgressDbEnsurer>();

        // Thread-safe singleton registry — populated at startup via Register<TContext>().
        builder.Services.TryAddSingleton<IMigrationCycleRegistry, MigrationCycleRegistry>();

        // Default no-op isolator. Applications using Tenant-per-Schema must register
        // their own ITenantDbIsolator BEFORE calling this method.
        builder.Services.TryAddSingleton<ITenantDbIsolator, NullTenantDbIsolator>();

        // Default no-op enumerator. Applications using Tenant-per-Schema or Tenant-per-Database
        // must register their own ITenantEnumerator BEFORE calling this method.
        builder.Services.TryAddSingleton<ITenantEnumerator, NullTenantEnumerator>();

        // Channel-based dispatch (default). Replaced by Granit.Persistence.Migrations.Wolverine if installed.
        builder.Services.TryAddSingleton(Channel.CreateUnbounded<RunMigrationBatchCommand>());
        builder.Services.TryAddSingleton<IMigrationBatchDispatcher, ChannelBatchDispatcher>();
        builder.Services.AddScoped<MigrationBatchExecutor>();
        builder.Services.AddHostedService<MigrationBatchWorker>();

        // Hosted service — resumes pending and in-progress cycles at startup.
        builder.Services.AddHostedService<MigrationStartupService>();

        // Options — bound from the "GranitMigrations" configuration section.
        builder.Services
            .AddOptions<MigrationStartupOptions>()
            .BindConfiguration(MigrationStartupOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return builder;
    }
}
