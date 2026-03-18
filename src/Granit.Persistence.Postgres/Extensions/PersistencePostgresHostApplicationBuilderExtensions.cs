using Granit.Persistence.Hosting;
using Granit.Persistence.Migrations;
using Granit.Persistence.MultiTenancy;
using Granit.Persistence.Postgres.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Persistence.Postgres.Extensions;

/// <summary>
/// Extension methods for registering PostgreSQL-specific persistence services.
/// </summary>
public static class PersistencePostgresHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers PostgreSQL-specific persistence services:
    /// <list type="bullet">
    ///   <item><see cref="IGranitMigrationLock"/> — <c>pg_try_advisory_lock</c>-backed distributed migration lock.</item>
    ///   <item><see cref="ITenantSchemaActivator"/> — executes <c>SET search_path</c> per tenant.</item>
    ///   <item><see cref="ITenantDbIsolator"/> — isolates a <c>DbContext</c> to a tenant schema before migrations.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// Call this before <c>AddGranitMigrateSupport()</c> and before any
    /// <c>AddTenantPerSchemaDbContext</c> call so that <c>TryAdd</c> registrations
    /// are not overridden by no-op fallbacks.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitPostgres(
        this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IGranitMigrationLock, NpgsqlAdvisoryMigrationLock>();
        builder.Services.TryAddSingleton<ITenantSchemaActivator, NpgsqlTenantSchemaActivator>();
        builder.Services.TryAddSingleton<ITenantDbIsolator, NpgsqlTenantDbIsolator>();
        return builder;
    }
}
